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
using Plugin.BumpDentDetect.Views;
using System;
using System.ComponentModel;
using System.Linq;
using VM.Halcon;
using VM.Halcon.Config;

namespace Plugin.BumpDentDetect.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
    }

    public enum eDefectType
    {
        [EnumDescription("凸起")] Bump,
        [EnumDescription("凹坑")] Dent,
        [EnumDescription("凸起&凹坑")] Both,
    }

    public enum eDisplayMode
    {
        [EnumDescription("原始图像")] Original,
        [EnumDescription("凸起检测")] BumpResult,
        [EnumDescription("凹坑检测")] DentResult,
    }

    [Category("3D")]
    [DisplayName("凸凹检测")]
    [ModuleImageName("BumpDentDetect")]
    [Serializable]
    public class BumpDentDetectViewModel : ModuleBase
    {
        #region Runtime cache
        [NonSerialized] private HImage _originalImage;
        [NonSerialized] private HImage _bumpImage;
        [NonSerialized] private HImage _dentImage;
        [NonSerialized] private HRegion _bumpRegion;
        [NonSerialized] private HRegion _dentRegion;
        #endregion

        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0) return;
            if (InputImageLinkText == null)
                InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            HObject chObj = null;
            HObject chRealObj = null;
            HObject openedObj = null;
            HObject closedObj = null;
            HImage bumpImage = null;
            HImage dentImage = null;
            HRegion bumpRegion = null;
            HRegion dentRegion = null;

            try
            {
                _originalImage?.Dispose(); _originalImage = null;
                _bumpImage?.Dispose(); _bumpImage = null;
                _dentImage?.Dispose(); _dentImage = null;
                _bumpRegion?.Dispose(); _bumpRegion = null;
                _dentRegion?.Dispose(); _dentRegion = null;

                ClearRoiAndText();

                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取输入图像
                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                _originalImage = new HImage(DispImage);

                // 提取高度通道
                HOperatorSet.CountChannels(_originalImage, out HTuple channelCount);
                if (channelCount.I < 1)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                if (channelCount == 2)
                    HOperatorSet.Decompose2(_originalImage, out chObj, out HObject _);
                else
                    HOperatorSet.AccessChannel(_originalImage, out chObj, 1);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");

                // Z分辨率缩放 → 物理高度
                if (Math.Abs(ResolutionZ - 1.0) > 1e-12)
                {
                    HOperatorSet.ScaleImage(new HImage(chRealObj), out HObject scaledObj, ResolutionZ, 0.0);
                    chRealObj.Dispose();
                    chRealObj = scaledObj;
                }

                HOperatorSet.GetImageSize(new HImage(chRealObj), out HTuple width, out HTuple height);

                // === 顶帽变换 ===

                // 核大小（必须 >= 1，且为奇数）
                int kH = Math.Max(1, KernelSize);
                int kW = Math.Max(1, KernelSize);
                if (kH % 2 == 0) kH++;
                if (kW % 2 == 0) kW++;

                // 白顶帽 = 原始 - 开运算 → 提取凸起
                HOperatorSet.GrayOpeningRect(new HImage(chRealObj), out openedObj, kH, kW);
                HOperatorSet.SubImage(new HImage(chRealObj), new HImage(openedObj), out HObject bumpObj, 1.0, 0.0);
                openedObj.Dispose();
                bumpImage = new HImage(bumpObj);
                _bumpImage = new HImage(bumpObj);
                bumpObj.Dispose();

                // 黑顶帽 = 闭运算 - 原始 → 提取凹坑
                HOperatorSet.GrayClosingRect(new HImage(chRealObj), out closedObj, kH, kW);
                HOperatorSet.SubImage(new HImage(closedObj), new HImage(chRealObj), out HObject dentObj, 1.0, 0.0);
                closedObj.Dispose();
                dentImage = new HImage(dentObj);
                _dentImage = new HImage(dentObj);
                dentObj.Dispose();

                // === 阈值检测 ===
                if (DefectType == eDefectType.Bump || DefectType == eDefectType.Both)
                {
                    HOperatorSet.Threshold(_bumpImage, out HObject bumpReg, Threshold, 999999.0);
                    bumpRegion = new HRegion(bumpReg);
                    bumpReg.Dispose();
                }

                if (DefectType == eDefectType.Dent || DefectType == eDefectType.Both)
                {
                    HOperatorSet.Threshold(_dentImage, out HObject dentReg, Threshold, 999999.0);
                    dentRegion = new HRegion(dentReg);
                    dentReg.Dispose();
                }

                _bumpRegion = bumpRegion?.IsInitialized() == true ? new HRegion(bumpRegion) : null;
                _dentRegion = dentRegion?.IsInitialized() == true ? new HRegion(dentRegion) : null;

                // === 统计 ===
                CalcStats(bumpRegion, dentRegion);

                // === 显示 ===
                UpdateDisplay();

                // === ROI显示 ===
                if (ShowRegion)
                {
                    if (_bumpRegion != null && _bumpRegion.IsInitialized() && _bumpRegion.Area > 0)
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                            HRoiType.检测结果, "red", new HObject(_bumpRegion)));
                    if (_dentRegion != null && _dentRegion.IsInitialized() && _dentRegion.Area > 0)
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                            HRoiType.检测结果, "blue", new HObject(_dentRegion)));
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
                chObj?.Dispose();
                chRealObj?.Dispose();
            }
        }

        private void CalcStats(HRegion bumpRegion, HRegion dentRegion)
        {
            BumpCount = 0; BumpArea = 0; MaxBumpHeight = 0;
            DentCount = 0; DentArea = 0; MaxDentDepth = 0;

            if (bumpRegion != null && bumpRegion.IsInitialized() && bumpRegion.Area > 0)
            {
                HOperatorSet.Connection(bumpRegion, out HObject conn);
                HOperatorSet.CountObj(conn, out HTuple cnt);
                HOperatorSet.AreaCenter(conn, out HTuple area, out _, out _);
                HOperatorSet.MinMaxGray(bumpRegion, _bumpImage, 0, out _, out HTuple maxV, out _);
                BumpCount = cnt.I;
                BumpArea = area.D;
                MaxBumpHeight = Math.Round(maxV.D, 6);
                conn.Dispose();
            }

            if (dentRegion != null && dentRegion.IsInitialized() && dentRegion.Area > 0)
            {
                HOperatorSet.Connection(dentRegion, out HObject conn);
                HOperatorSet.CountObj(conn, out HTuple cnt);
                HOperatorSet.AreaCenter(conn, out HTuple area, out _, out _);
                HOperatorSet.MinMaxGray(dentRegion, _dentImage, 0, out HTuple minV, out _, out _);
                DentCount = cnt.I;
                DentArea = area.D;
                MaxDentDepth = Math.Round(minV.D, 6);
                conn.Dispose();
            }
        }

        private void UpdateDisplay()
        {
            try
            {
                switch (DisplayMode)
                {
                    case eDisplayMode.Original:
                        if (_originalImage != null && _originalImage.IsInitialized())
                            DispImage = new RImage(new HImage(_originalImage));
                        break;
                    case eDisplayMode.BumpResult:
                        if (_bumpImage != null && _bumpImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_bumpImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;
                    case eDisplayMode.DentResult:
                        if (_dentImage != null && _dentImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_dentImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;
                }
                if (ModuleView != null && ModuleView.mWindowH != null)
                    ModuleView.mWindowH.HobjectToHimage(DispImage);
            }
            catch { }
        }

        private HImage ScaleToByte(HImage realImage)
        {
            try
            {
                HOperatorSet.MinMaxGray(realImage.GetDomain(), realImage, 0, out HTuple minV, out HTuple maxV, out HTuple range);
                if (range.D <= 0)
                {
                    HOperatorSet.ConvertImageType(realImage, out HObject b, "byte");
                    return new HImage(b);
                }
                HImage sc = realImage.ScaleImage(255.0 / range.D, -minV.D * 255.0 / range.D);
                HOperatorSet.ConvertImageType(sc, out HObject b2, "byte");
                sc.Dispose();
                return new HImage(b2);
            }
            catch { return null; }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("凸起区域", "HRegion", _bumpRegion);
            AddOutputParam("凹坑区域", "HRegion", _dentRegion);
            AddOutputParam("凸起数量", "int", BumpCount);
            AddOutputParam("凸起面积", "double", BumpArea);
            AddOutputParam("最大凸起高度", "double", MaxBumpHeight);
            AddOutputParam("凹坑数量", "int", DentCount);
            AddOutputParam("凹坑面积", "double", DentArea);
            AddOutputParam("最大凹坑深度", "double", MaxDentDepth);
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

        private int _KernelSize = 31;
        public int KernelSize
        {
            get { return _KernelSize; }
            set { Set(ref _KernelSize, Math.Max(3, value)); }
        }

        private eDefectType _DefectType = eDefectType.Both;
        public eDefectType DefectType
        {
            get { return _DefectType; }
            set { Set(ref _DefectType, value); }
        }

        private double _Threshold = 0.10;
        public double Threshold
        {
            get { return _Threshold; }
            set { Set(ref _Threshold, value); }
        }

        private bool _ShowRegion = true;
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        private eDisplayMode _DisplayMode = eDisplayMode.Original;
        public eDisplayMode DisplayMode
        {
            get { return _DisplayMode; }
            set
            {
                Set(ref _DisplayMode, value);
                UpdateDisplay();
            }
        }

        private int _BumpCount;
        public int BumpCount { get { return _BumpCount; } set { Set(ref _BumpCount, value); } }
        private double _BumpArea;
        public double BumpArea { get { return _BumpArea; } set { Set(ref _BumpArea, value); } }
        private double _MaxBumpHeight;
        public double MaxBumpHeight { get { return _MaxBumpHeight; } set { Set(ref _MaxBumpHeight, value); } }
        private int _DentCount;
        public int DentCount { get { return _DentCount; } set { Set(ref _DentCount, value); } }
        private double _DentArea;
        public double DentArea { get { return _DentArea; } set { Set(ref _DentArea, value); } }
        private double _MaxDentDepth;
        public double MaxDentDepth { get { return _MaxDentDepth; } set { Set(ref _MaxDentDepth, value); } }
        #endregion

        #region Commands
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as BumpDentDetectView;
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
            if (linkCommand == eLinkCommand.InputImageLink)
                InputImageLinkText = obj.LinkName;
        }

        [NonSerialized] private CommandBase _LinkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        if ((eLinkCommand)obj == eLinkCommand.InputImageLink)
                        {
                            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                            EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                        }
                    });
                }
                return _LinkCommand;
            }
        }

        [NonSerialized] private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                    _ExecuteCommand = new CommandBase((obj) => { ExeModule(); });
                return _ExecuteCommand;
            }
        }

        [NonSerialized] private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as BumpDentDetectView;
                        if (view != null) view.Close();
                    });
                return _ConfirmCommand;
            }
        }

        [NonSerialized] private CommandBase _SwitchDisplayCommand;
        public CommandBase SwitchDisplayCommand
        {
            get
            {
                if (_SwitchDisplayCommand == null)
                    _SwitchDisplayCommand = new CommandBase((obj) =>
                    {
                        if (obj is eDisplayMode mode) DisplayMode = mode;
                    });
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
            obj["KernelSize"] = KernelSize;
            obj["DefectType"] = (int)DefectType;
            obj["Threshold"] = Threshold;
            obj["ShowRegion"] = ShowRegion;
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
                if (obj["KernelSize"] != null) KernelSize = obj["KernelSize"].Value<int>();
                if (obj["DefectType"] != null) DefectType = (eDefectType)obj["DefectType"].Value<int>();
                if (obj["Threshold"] != null) Threshold = obj["Threshold"].Value<double>();
                if (obj["ShowRegion"] != null) ShowRegion = obj["ShowRegion"].Value<bool>();
                if (obj["DisplayMode"] != null) DisplayMode = (eDisplayMode)obj["DisplayMode"].Value<int>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"BumpDentDetectViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
