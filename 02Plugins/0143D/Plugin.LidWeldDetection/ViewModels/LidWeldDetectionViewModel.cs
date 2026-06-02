using EventMgrLib;
using HalconDotNet;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using Newtonsoft.Json.Linq;
using Plugin.LidWeldDetection.Views;
using System;
using System.ComponentModel;
using System.Linq;
using VM.Halcon;
using VM.Halcon.Config;

namespace Plugin.LidWeldDetection.ViewModels
{
    #region enums
    public enum eLinkCommand
    {
        InputImageLink,
    }

    public enum eInterpolationMethod
    {
        [EnumDescription("线性邻插值")] Bilinear,
        [EnumDescription("最近邻插值")] NearestNeighbor,
        [EnumDescription("cubic B插值")] Bicubic,
        [EnumDescription("Catmull-Rom样条插值")] CatmullRom,
        [EnumDescription("natural cubic插值")] NaturalCubic,
    }

    public enum eDefectType
    {
        [EnumDescription("凹")] Concave,
        [EnumDescription("凸")] Convex,
        [EnumDescription("凹&凸")] Both,
    }

    public enum eDisplayMode
    {
        [EnumDescription("原始图像")] Original,
        [EnumDescription("拟合结果")] Fitted,
        [EnumDescription("差值结果")] Difference,
        [EnumDescription("检测结果")] Detection,
    }
    #endregion

    [Category("3D")]
    [DisplayName("顶盖焊检测")]
    [ModuleImageName("LidWeldDetection")]
    [Serializable]
    public class LidWeldDetectionViewModel : ModuleBase
    {
        #region Runtime image caches
        [NonSerialized] private HImage _originalImage;
        [NonSerialized] private HImage _fittedImage;
        [NonSerialized] private HImage _diffImage;
        [NonSerialized] private HRegion _defectRegion;
        [NonSerialized] private HRegion _concaveRegion;
        [NonSerialized] private HRegion _convexRegion;
        #endregion

        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
            {
                return;
            }
            if (InputImageLinkText == null)
                InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            HImage sourceImage = null;
            HObject chObj = null;
            HObject chRealObj = null;
            HObject sampledObj = null;
            HObject fittedObj = null;
            HImage fittedImage = null;
            HObject diffObj = null;
            HImage diffImage = null;
            HRegion defectRegion = null;
            HRegion concaveRegion = null;
            HRegion convexRegion = null;
            HRegion validRegion = null;

            try
            {
                // 释放旧的缓存图像
                _originalImage?.Dispose(); _originalImage = null;
                _fittedImage?.Dispose(); _fittedImage = null;
                _diffImage?.Dispose(); _diffImage = null;
                _defectRegion?.Dispose(); _defectRegion = null;
                _concaveRegion?.Dispose(); _concaveRegion = null;
                _convexRegion?.Dispose(); _convexRegion = null;

                ClearRoiAndText();

                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // === 获取输入图像 ===
                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                sourceImage = new HImage(DispImage);
                _originalImage = new HImage(DispImage);

                // 提取高度通道（通道1），转 real 类型
                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (channelCount.I < 1)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("图像无有效通道"));
                    return false;
                }
                HOperatorSet.AccessChannel(sourceImage, out chObj, 1);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");

                // Z分辨率缩放：像素值 × ResolutionZ → 物理高度
                if (Math.Abs(ResolutionZ - 1.0) > 1e-12)
                {
                    HOperatorSet.ScaleImage(new HImage(chRealObj), out HObject scaledObj, ResolutionZ, 0.0);
                    chRealObj.Dispose();
                    chRealObj = scaledObj;
                }

                // 获取原图尺寸
                HOperatorSet.GetImageSize(chRealObj, out HTuple width, out HTuple height);
                int srcW = width.I;
                int srcH = height.I;

                // === 过滤无效数据 ===
                // 高度 <= 0 或 >= 999999 视为无效点（飞点/量程外）
                HOperatorSet.Threshold(chRealObj, out HObject validRegionObj, 0.0001, 999999.0);
                validRegion = new HRegion(validRegionObj);
                validRegionObj.Dispose();

                // 计算有效区域的平均高度，用于填充无效点
                HOperatorSet.Intensity(validRegion, new HImage(chRealObj), out HTuple meanHeight, out _);
                double fillValue = meanHeight.D;

                // 用平均值填充无效区域，避免无效点污染后续插值
                HOperatorSet.PaintRegion(validRegion, new HImage(chRealObj), out HObject filledObj, (HTuple)fillValue, "fill");
                HImage filledImage = new HImage(filledObj);
                filledObj.Dispose();

                // === 降采样 ===
                // 采样间隔：每隔 N 个像素取 1 个点
                // 目标尺寸 = 原尺寸 / 采样间隔（向上取整，至少保留 2 个像素）
                int intervalX = Math.Max(1, SampleRateX);
                int intervalY = Math.Max(1, SampleRateY);
                int targetW = Math.Max(2, (int)Math.Ceiling((double)srcW / intervalX));
                int targetH = Math.Max(2, (int)Math.Ceiling((double)srcH / intervalY));
                string interpDown = "constant"; // 降采样统一用最近邻取点
                string interpUp = GetHalconInterpolationMode(); // 上采样用用户选择的方法

                HOperatorSet.ZoomImageSize(filledImage, out sampledObj, targetW, targetH, interpDown);
                filledImage.Dispose();

                // === 上采样回原尺寸 → 拟合曲面 ===
                HOperatorSet.ZoomImageSize(sampledObj, out fittedObj, srcW, srcH, interpUp);
                fittedImage = new HImage(fittedObj);
                _fittedImage = new HImage(fittedObj);

                // === 差值图 = 原始 - 拟合（只在有效区域计算）===
                HOperatorSet.ReduceDomain(new HImage(chRealObj), validRegion, out HObject validRealObj);
                HImage validRealImage = new HImage(validRealObj);
                HOperatorSet.ReduceDomain(fittedImage, validRegion, out HObject validFittedObj);
                HImage validFittedImage = new HImage(validFittedObj);
                HOperatorSet.SubImage(validRealImage, validFittedImage, out diffObj, 1.0, 0.0);
                validRealImage.Dispose();
                validFittedImage.Dispose();
                diffImage = new HImage(diffObj);
                _diffImage = new HImage(diffObj);

                // === 缺陷检测 ===
                bool hasConcave = false;
                bool hasConvex = false;

                if (DefectType == eDefectType.Concave || DefectType == eDefectType.Both)
                {
                    // 凹缺陷：差值 < -凹陷阈值，且必须在有效区域内
                    HOperatorSet.Threshold(diffImage, out HObject concaveObj, -999999.0, -ConcaveThreshold);
                    HOperatorSet.Intersection(new HRegion(concaveObj), validRegion, out HObject concaveValidObj);
                    concaveObj.Dispose();
                    concaveRegion = new HRegion(concaveValidObj);
                    concaveValidObj.Dispose();
                    if (concaveRegion.IsInitialized() && concaveRegion.Area > 0)
                        hasConcave = true;
                }

                if (DefectType == eDefectType.Convex || DefectType == eDefectType.Both)
                {
                    // 凸缺陷：差值 > 凸起阈值，且必须在有效区域内
                    HOperatorSet.Threshold(diffImage, out HObject convexObj, ConvexThreshold, 999999.0);
                    HOperatorSet.Intersection(new HRegion(convexObj), validRegion, out HObject convexValidObj);
                    convexObj.Dispose();
                    convexRegion = new HRegion(convexValidObj);
                    convexValidObj.Dispose();
                    if (convexRegion.IsInitialized() && convexRegion.Area > 0)
                        hasConvex = true;
                }

                // 合并缺陷区域
                if (DefectType == eDefectType.Both && hasConcave && hasConvex)
                {
                    HOperatorSet.Union2(concaveRegion, convexRegion, out HObject unionObj);
                    defectRegion = new HRegion(unionObj);
                    unionObj.Dispose();
                }
                else if (DefectType == eDefectType.Concave && hasConcave)
                {
                    defectRegion = new HRegion(concaveRegion);
                }
                else if (DefectType == eDefectType.Convex && hasConvex)
                {
                    defectRegion = new HRegion(convexRegion);
                }
                else
                {
                    defectRegion = new HRegion();
                }

                _defectRegion = defectRegion != null && defectRegion.IsInitialized() ? new HRegion(defectRegion) : null;
                _concaveRegion = concaveRegion != null && concaveRegion.IsInitialized() ? new HRegion(concaveRegion) : null;
                _convexRegion = convexRegion != null && convexRegion.IsInitialized() ? new HRegion(convexRegion) : null;

                // === 统计缺陷信息 ===
                CalculateDefectStats(diffImage, concaveRegion, convexRegion);

                // === 根据显示模式更新 DispImage ===
                UpdateDisplayImage();

                // === 显示缺陷区域 ===
                if (ShowDefectRegion)
                {
                    if (_concaveRegion != null && _concaveRegion.IsInitialized() && _concaveRegion.Area > 0)
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "blue",
                            new HObject(_concaveRegion)
                        ));
                    }
                    if (_convexRegion != null && _convexRegion.IsInitialized() && _convexRegion.Area > 0)
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "red",
                            new HObject(_convexRegion)
                        ));
                    }
                }

                ShowHRoi();

                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
            finally
            {
                sourceImage?.Dispose();
                chObj?.Dispose();
                chRealObj?.Dispose();
                sampledObj?.Dispose();
                fittedObj?.Dispose();
                fittedImage?.Dispose();
                diffObj?.Dispose();
                diffImage?.Dispose();
                validRegion?.Dispose();
                // defectRegion, concaveRegion, convexRegion 已保存到 _xxxRegion，不在此处释放
            }
        }

        private void CalculateDefectStats(HImage diffImage, HRegion concaveRegion, HRegion convexRegion)
        {
            DefectCount = 0;
            DefectArea = 0;
            MaxConcaveDepth = 0;
            MaxConvexHeight = 0;

            // 凹缺陷统计
            if (concaveRegion != null && concaveRegion.IsInitialized() && concaveRegion.Area > 0)
            {
                HOperatorSet.Connection(concaveRegion, out HObject connectedConcave);
                HOperatorSet.CountObj(connectedConcave, out HTuple ccCount);
                HOperatorSet.AreaCenter(connectedConcave, out HTuple ccArea, out _, out _);
                HOperatorSet.MinMaxGray(concaveRegion, diffImage, 0, out HTuple minVal, out _, out _);

                DefectCount += ccCount.I;
                DefectArea += ccArea.D;
                if (minVal.D < 0)
                    MaxConcaveDepth = Math.Round(-minVal.D, 6);

                connectedConcave.Dispose();
            }

            // 凸缺陷统计
            if (convexRegion != null && convexRegion.IsInitialized() && convexRegion.Area > 0)
            {
                HOperatorSet.Connection(convexRegion, out HObject connectedConvex);
                HOperatorSet.CountObj(connectedConvex, out HTuple cvCount);
                HOperatorSet.AreaCenter(connectedConvex, out HTuple cvArea, out _, out _);
                HOperatorSet.MinMaxGray(convexRegion, diffImage, 0, out _, out HTuple maxVal, out _);

                DefectCount += cvCount.I;
                DefectArea += cvArea.D;
                MaxConvexHeight = Math.Round(maxVal.D, 6);

                connectedConvex.Dispose();
            }
        }

        private void UpdateDisplayImage()
        {
            try
            {
                switch (DisplayMode)
                {
                    case eDisplayMode.Original:
                        if (_originalImage != null && _originalImage.IsInitialized())
                            DispImage = new RImage(new HImage(_originalImage));
                        break;

                    case eDisplayMode.Fitted:
                        if (_fittedImage != null && _fittedImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_fittedImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;

                    case eDisplayMode.Difference:
                        if (_diffImage != null && _diffImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_diffImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;

                    case eDisplayMode.Detection:
                        // 检测结果：显示差值图伪彩色 + 缺陷标记
                        if (_diffImage != null && _diffImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_diffImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;
                }

                if (ModuleView != null && ModuleView.mWindowH != null)
                    ModuleView.mWindowH.HobjectToHimage(DispImage);
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }

        /// <summary>将 real 图像缩放到 0-255 byte 范围用于显示</summary>
        private HImage ScaleToByte(HImage realImage)
        {
            try
            {
                HOperatorSet.MinMaxGray(realImage.GetDomain(), realImage, 0, out HTuple minVal, out HTuple maxVal, out HTuple range);
                if (range.D <= 0)
                {
                    HOperatorSet.ConvertImageType(realImage, out HObject byteObj, "byte");
                    return new HImage(byteObj);
                }
                HImage scaled = realImage.ScaleImage(255.0 / range.D, -minVal.D * 255.0 / range.D);
                HOperatorSet.ConvertImageType(scaled, out HObject byteObj2, "byte");
                scaled.Dispose();
                return new HImage(byteObj2);
            }
            catch
            {
                return null;
            }
        }

        private string GetHalconInterpolationMode()
        {
            switch (InterpolationMethod)
            {
                case eInterpolationMethod.NearestNeighbor: return "constant";
                case eInterpolationMethod.Bilinear: return "bilinear";
                case eInterpolationMethod.Bicubic: return "bicubic";
                case eInterpolationMethod.CatmullRom: return "bicubic"; // 占位，后续可扩展
                case eInterpolationMethod.NaturalCubic: return "bicubic"; // 占位，后续可扩展
                default: return "bilinear";
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("缺陷区域", "HRegion", _defectRegion);
            AddOutputParam("缺陷数量", "int", DefectCount);
            AddOutputParam("缺陷面积", "double", DefectArea);
            AddOutputParam("最大凹陷深度", "double", MaxConcaveDepth);
            AddOutputParam("最大凸起高度", "double", MaxConvexHeight);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Properties
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private double _ResolutionZ = 1.0;
        public double ResolutionZ
        {
            get { return _ResolutionZ; }
            set { Set(ref _ResolutionZ, value); }
        }

        private int _SampleRateX = 100;
        public int SampleRateX
        {
            get { return _SampleRateX; }
            set { Set(ref _SampleRateX, Math.Max(2, value)); }
        }

        private int _SampleRateY = 100;
        public int SampleRateY
        {
            get { return _SampleRateY; }
            set { Set(ref _SampleRateY, Math.Max(2, value)); }
        }

        private eInterpolationMethod _InterpolationMethod = eInterpolationMethod.Bilinear;
        public eInterpolationMethod InterpolationMethod
        {
            get { return _InterpolationMethod; }
            set { Set(ref _InterpolationMethod, value); }
        }

        private eDefectType _DefectType = eDefectType.Concave;
        public eDefectType DefectType
        {
            get { return _DefectType; }
            set { Set(ref _DefectType, value); }
        }

        private double _ConcaveThreshold = 0.20;
        public double ConcaveThreshold
        {
            get { return _ConcaveThreshold; }
            set { Set(ref _ConcaveThreshold, value); }
        }

        private double _ConvexThreshold = 0.20;
        public double ConvexThreshold
        {
            get { return _ConvexThreshold; }
            set { Set(ref _ConvexThreshold, value); }
        }

        private bool _ShowDefectRegion = true;
        public bool ShowDefectRegion
        {
            get { return _ShowDefectRegion; }
            set { Set(ref _ShowDefectRegion, value); }
        }

        private eDisplayMode _DisplayMode = eDisplayMode.Original;
        public eDisplayMode DisplayMode
        {
            get { return _DisplayMode; }
            set
            {
                Set(ref _DisplayMode, value);
                UpdateDisplayImage();
            }
        }

        // 输出结果
        private int _DefectCount;
        public int DefectCount
        {
            get { return _DefectCount; }
            set { Set(ref _DefectCount, value); }
        }

        private double _DefectArea;
        public double DefectArea
        {
            get { return _DefectArea; }
            set { Set(ref _DefectArea, value); }
        }

        private double _MaxConcaveDepth;
        public double MaxConcaveDepth
        {
            get { return _MaxConcaveDepth; }
            set { Set(ref _MaxConcaveDepth, value); }
        }

        private double _MaxConvexHeight;
        public double MaxConvexHeight
        {
            get { return _MaxConvexHeight; }
            set { Set(ref _MaxConvexHeight, value); }
        }
        #endregion

        #region Commands
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as LidWeldDetectionView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                GetDispImage(InputImageLinkText, true);
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1]);
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
            }
        }

        [NonSerialized]
        private CommandBase _LinkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged,
                        o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InputImageLink");
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }

        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                    _ExecuteCommand = new CommandBase((obj) => { ExeModule(); });
                return _ExecuteCommand;
            }
        }

        [NonSerialized]
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as LidWeldDetectionView;
                        if (view != null) view.Close();
                    });
                }
                return _ConfirmCommand;
            }
        }

        [NonSerialized]
        private CommandBase _SwitchDisplayCommand;
        public CommandBase SwitchDisplayCommand
        {
            get
            {
                if (_SwitchDisplayCommand == null)
                {
                    _SwitchDisplayCommand = new CommandBase((obj) =>
                    {
                        if (obj is eDisplayMode mode)
                            DisplayMode = mode;
                    });
                }
                return _SwitchDisplayCommand;
            }
        }
        #endregion

        #region Serialization
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["ResolutionZ"] = ResolutionZ;
            obj["SampleRateX"] = SampleRateX;
            obj["SampleRateY"] = SampleRateY;
            obj["InterpolationMethod"] = (int)InterpolationMethod;
            obj["DefectType"] = (int)DefectType;
            obj["ConcaveThreshold"] = ConcaveThreshold;
            obj["ConvexThreshold"] = ConvexThreshold;
            obj["ShowDefectRegion"] = ShowDefectRegion;
            obj["DisplayMode"] = (int)DisplayMode;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["ResolutionZ"] != null) ResolutionZ = obj["ResolutionZ"].Value<double>();
                if (obj["SampleRateX"] != null) SampleRateX = obj["SampleRateX"].Value<int>();
                if (obj["SampleRateY"] != null) SampleRateY = obj["SampleRateY"].Value<int>();
                if (obj["InterpolationMethod"] != null) InterpolationMethod = (eInterpolationMethod)obj["InterpolationMethod"].Value<int>();
                if (obj["DefectType"] != null) DefectType = (eDefectType)obj["DefectType"].Value<int>();
                if (obj["ConcaveThreshold"] != null) ConcaveThreshold = obj["ConcaveThreshold"].Value<double>();
                if (obj["ConvexThreshold"] != null) ConvexThreshold = obj["ConvexThreshold"].Value<double>();
                if (obj["ShowDefectRegion"] != null) ShowDefectRegion = obj["ShowDefectRegion"].Value<bool>();
                if (obj["DisplayMode"] != null) DisplayMode = (eDisplayMode)obj["DisplayMode"].Value<int>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"LidWeldDetectionViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
