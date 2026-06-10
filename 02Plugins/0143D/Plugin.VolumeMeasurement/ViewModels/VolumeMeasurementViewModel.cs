using EventMgrLib;
using HalconDotNet;
using Newtonsoft.Json.Linq;
using Plugin.VolumeMeasurement.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
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
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;

namespace Plugin.VolumeMeasurement.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        BasePlaneLink,
        MeasureRegionLink,
        RemoveHeight,
        ResolutionX,
        ResolutionY,
        ResolutionZ,
    }

    public enum eVolumeCalcMode
    {
        [EnumDescription("平面上方")]
        AbovePlane,
        [EnumDescription("平面下方")]
        BelowPlane,
    }

    public enum eMeasureRegionSource
    {
        [EnumDescription("整图")]
        FullImage,
        [EnumDescription("手动绘制")]
        Manual,
        [EnumDescription("链接区域")]
        LinkRegion,
    }

    [Category("3D")]
    [DisplayName("体积计算")]
    [ModuleImageName("VolumeMeasurement")]
    [Serializable]
    public class VolumeMeasurementViewModel : ModuleBase
    {
        private const int HeightChannel = 1;

        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
                return;

            if (InputImageLinkText == null)
                InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        public override bool ExeModule()
        {
            // 记录本次模块执行耗时，最终由基类写入 ModuleParam.ElapsedTime。
            Stopwatch.Restart();

            // 这些 Halcon 对象在函数末尾统一 Dispose，避免图像/区域句柄泄漏。
            HImage sourceImage = null;
            HObject heightObj = null;
            HObject heightRealObj = null;
            HObject basePlaneObj = null;
            HObject diffObj = null;
            HObject selectedObj = null;
            HObject actualHeightObj = null;
            HObject roiHeightObj = null;
            HObject validRegionObj = null;
            HRegion imageRegion = null;
            HRegion measureRegion = null;
            HRegion clippedRegion = null;
            bool disposeMeasureRegion = false;

            try
            {
                // 每次执行前先清掉当前模块上一次显示的 ROI/文字结果，再重新计算和显示。
                ClearRoiAndText();
                Volume = 0;

                // 输入图像是体积计算的必需输入，没有链接图像就直接 NG。
                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 非打开窗口状态下，从输入链接里取图像；打开窗口时通常已经由界面加载过 DispImage。
                if (!IsOpenWindows)
                    GetDispImage(InputImageLinkText, true);

                // DispImage 是后续所有高度值、ROI 和显示的基础，必须保证有效。
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 将输入图像转成 Halcon HImage，并检查高度通道是否存在。
                // 当前约定第 1 通道是高度图，如果上游图像通道数不足就无法计算体积。
                sourceImage = new HImage(DispImage);
                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (HeightChannel > channelCount.I)
                {
                    Logger.GetExceptionMsg(new Exception($"体积计算输入图像通道数不足，当前通道数 {channelCount.I}。"));
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 取出高度通道并转成 real 类型，后面要做减法、缩放和浮点体积累加。
                HOperatorSet.AccessChannel(sourceImage, out heightObj, HeightChannel);
                HOperatorSet.ConvertImageType(heightObj, out heightRealObj, "real");
                HOperatorSet.GetImageSize(heightRealObj, out HTuple width, out HTuple height);

                // 生成整张图像范围的矩形区域，用来限制测量 ROI 不越界。
                // Halcon 图像坐标从 0 开始，所以最后一行是 height - 1，最后一列是 width - 1。
                // 例如高度为 100 时，有效行坐标是 0~99，而不是 0~100。
                HOperatorSet.GenRectangle1(
                    out HObject imageRegionObj,
                    new HTuple(0),
                    new HTuple(0),
                    new HTuple(height.I - 1),
                    new HTuple(width.I - 1));
                imageRegion = new HRegion(imageRegionObj);

                // 获取用户设置的测量区域：整图、手动绘制 ROI 或链接的 HRegion。
                // 再与整图区域求交集，保证后续图像统计只在有效图像坐标内进行。
                measureRegion = GetMeasureRegion(out disposeMeasureRegion);
                clippedRegion = ClipRegionToImage(measureRegion, imageRegion);
                if (clippedRegion == null || !clippedRegion.IsInitialized() || clippedRegion.Area <= 0)
                {
                    Logger.AddLog("体积计算测量区域为空或超出图像范围。", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 构建基准平面图：有基准平面链接时使用链接平面，否则默认使用 Z=0 平面。
                // diffObj = 高度图 - 基准平面，表示每个像素相对基准面的高度差。
                basePlaneObj = BuildBasePlane(heightRealObj, width.I, height.I);
                HOperatorSet.SubImage(heightRealObj, basePlaneObj, out diffObj, 1.0, 0.0);

                // 平面上方：直接统计正向高度差。
                // 平面下方：把高度差乘以 -1，把凹陷深度转成正值后再统计。
                if (CalcMode == eVolumeCalcMode.BelowPlane)
                    HOperatorSet.ScaleImage(diffObj, out selectedObj, -1.0, 0.0);
                else
                    HOperatorSet.CopyImage(diffObj, out selectedObj);

                // 读取界面参数：
                // removeHeight 是剔除高度阈值，用来过滤基准面附近的小噪声/小毛刺。
                // resolutionX/Y 是单个像素在 X/Y 方向代表的实际尺寸，单位通常是 mm/px。
                // resolutionZ 是高度图灰度值到实际高度的换算比例，单位通常是 mm/unit。
                double removeHeight = Math.Max(0, ResolveLinkValue(RemoveHeight));
                double resolutionX = Math.Max(0, ResolveLinkValue(ResolutionX));
                double resolutionY = Math.Max(0, ResolveLinkValue(ResolutionY));
                double resolutionZ = Math.Max(0, ResolveLinkValue(ResolutionZ));
                if (resolutionX <= 0 || resolutionY <= 0 || resolutionZ <= 0)
                {
                    Logger.AddLog("体积计算 X/Y/Z 分辨率必须大于 0。", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 用 Halcon 图像算子在内部完成有效高度筛选和求和，避免把大 ROI 的每个像素值搬到 C# 循环。
                // 实际高度图 = 高度差图像值 * Z 分辨率 - 剔除高度；只有大于 0 的点参与体积。
                HOperatorSet.ScaleImage(selectedObj, out actualHeightObj, resolutionZ, -removeHeight);
                HOperatorSet.ReduceDomain(actualHeightObj, clippedRegion, out roiHeightObj);

                double sumHeight = 0;
                int validCount = 0;
                HOperatorSet.MinMaxGray(clippedRegion, actualHeightObj, 0, out _, out HTuple maxHeight, out _);
                if (maxHeight.Length > 0 && maxHeight.D > 0)
                {
                    const double minPositiveHeight = 1e-12;
                    HOperatorSet.Threshold(roiHeightObj, out validRegionObj, minPositiveHeight, maxHeight);
                    HOperatorSet.AreaCenter(validRegionObj, out HTuple area, out _, out _);
                    if (area.Length > 0 && area.D > 0)
                    {
                        HOperatorSet.Intensity(validRegionObj, actualHeightObj, out HTuple meanHeight, out _);
                        sumHeight = meanHeight.D * area.D;
                        validCount = (int)Math.Round(area.D);
                    }
                }

                // 体积 = 所有有效点实际高度之和 * 单像素面积。
                // 单像素面积 = X 分辨率 * Y 分辨率；如果单位是 mm，则体积单位就是 mm^3。
                Volume = Math.Round(sumHeight * resolutionX * resolutionY, 6);
                ValidPointCount = validCount;

                // 按显示设置叠加测量区域和体积文字，方便在右侧窗口确认实际统计范围。
                if (ShowRegion)
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "green",
                        new HObject(clippedRegion)));

                    ShowHRoi(new HText(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.文字显示,
                        "green",
                        $"体积:{Volume:F3}{VolumeUnit}",
                        20,
                        20,
                        16));
                }

                // 刷新 ROI/文字显示并标记模块执行成功。
                ShowHRoi();
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                // 任意 Halcon 或链接取值异常都记日志，并把模块状态置为 NG。
                Logger.GetExceptionMsg(ex);
                ChangeModuleRunStatus(eRunStatus.NG);
                return false;
            }
            finally
            {
                // 释放本函数内创建的临时 Halcon 对象；clippedRegion 被用于显示，不能在这里 Dispose。
                sourceImage?.Dispose();
                heightObj?.Dispose();
                heightRealObj?.Dispose();
                basePlaneObj?.Dispose();
                diffObj?.Dispose();
                selectedObj?.Dispose();
                actualHeightObj?.Dispose();
                roiHeightObj?.Dispose();
                validRegionObj?.Dispose();
                imageRegion?.Dispose();
                if (disposeMeasureRegion)
                    measureRegion?.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("体积", "double", Volume);
            AddOutputParam("体积单位", "string", VolumeUnit);
            AddOutputParam("有效点数", "int", ValidPointCount);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        private HObject BuildBasePlane(HObject heightRealObj, int width, int height)
        {
            if (!string.IsNullOrEmpty(BasePlaneLinkText))
            {
                var savedDisp = DispImage;
                HImage linkedPlaneImg = null;
                HObject planeChObj = null;
                HObject planeRealObj = null;
                HObject planeObj = null;
                try
                {
                    GetDispImage(BasePlaneLinkText, true);
                    if (DispImage == null || !DispImage.IsInitialized())
                        throw new Exception("体积计算基准平面图像为空。");

                    linkedPlaneImg = new HImage(DispImage);
                    HOperatorSet.CountChannels(linkedPlaneImg, out HTuple channelCount);
                    if (HeightChannel > channelCount.I)
                        throw new Exception($"体积计算基准平面图像通道数不足，当前通道数 {channelCount.I}。");

                    HOperatorSet.AccessChannel(linkedPlaneImg, out planeChObj, HeightChannel);
                    HOperatorSet.ConvertImageType(planeChObj, out planeRealObj, "real");
                    HOperatorSet.FitSurfaceFirstOrder(
                        linkedPlaneImg.GetDomain(),
                        planeRealObj,
                        "regression",
                        5,
                        0.1,
                        out HTuple alpha,
                        out HTuple beta,
                        out HTuple gamma);
                    HOperatorSet.AreaCenter(linkedPlaneImg.GetDomain(), out _, out HTuple refRow, out HTuple refCol);
                    HOperatorSet.GenImageSurfaceFirstOrder(
                        out planeObj,
                        "real",
                        alpha,
                        beta,
                        gamma,
                        refRow,
                        refCol,
                        width,
                        height);
                    HOperatorSet.CopyImage(planeObj, out HObject result);
                    return result;
                }
                finally
                {
                    DispImage = savedDisp;
                    if (ModuleView != null && ModuleView.mWindowH != null && DispImage != null && DispImage.IsInitialized())
                        ModuleView.mWindowH.HobjectToHimage(DispImage);
                    linkedPlaneImg?.Dispose();
                    planeChObj?.Dispose();
                    planeRealObj?.Dispose();
                    planeObj?.Dispose();
                }
            }

            HOperatorSet.GenImageConst(out HObject zeroPlane, "real", width, height);
            HOperatorSet.ScaleImage(zeroPlane, out HObject basePlane, 0.0, 0.0);
            zeroPlane.Dispose();
            return basePlane;
        }

        private HRegion GetMeasureRegion(out bool shouldDispose)
        {
            shouldDispose = false;

            if (MeasureRegionSource == eMeasureRegionSource.LinkRegion && !string.IsNullOrEmpty(MeasureRegionLinkText))
            {
                // 链接区域属于上游模块，这里只借用，不能在 ExeModule finally 里 Dispose。
                return (HRegion)GetLinkValue(MeasureRegionLinkText);
            }

            if (MeasureRegionSource == eMeasureRegionSource.Manual &&
                TranMeasureRoi.Length1 > 0 &&
                TranMeasureRoi.Length2 > 0)
            {
                HRegion region = new HRegion();
                region.GenRectangle2(
                    TranMeasureRoi.MidR,
                    TranMeasureRoi.MidC,
                    -TranMeasureRoi.Phi,
                    TranMeasureRoi.Length1,
                    TranMeasureRoi.Length2);
                shouldDispose = true;
                return region;
            }

            if (DispImage == null || !DispImage.IsInitialized())
                return null;

            shouldDispose = true;
            return DispImage.GetDomain();
        }

        private HRegion ClipRegionToImage(HRegion region, HRegion imageRegion)
        {
            if (region == null)
            {
                Logger.AddLog("体积计算测量区域为空：region == null。", eMsgType.Warn);
                return null;
            }

            if (!region.IsInitialized())
            {
                Logger.AddLog("体积计算测量区域无效：region 未初始化。", eMsgType.Warn);
                return null;
            }

            if (imageRegion == null)
            {
                Logger.AddLog("体积计算图像范围为空：imageRegion == null。", eMsgType.Warn);
                return null;
            }

            if (!imageRegion.IsInitialized())
            {
                Logger.AddLog("体积计算图像范围无效：imageRegion 未初始化。", eMsgType.Warn);
                return null;
            }

            HOperatorSet.Intersection(region, imageRegion, out HObject clippedObj);
            return new HRegion(clippedObj);
        }

        private void InitMeasureRoiMethod()
        {
            var view = ModuleView as VolumeMeasurementView;
            if (view == null || view.mWindowH == null)
                return;

            if (MeasureRegionSource == eMeasureRegionSource.LinkRegion && !string.IsNullOrEmpty(MeasureRegionLinkText))
            {
                HRegion region = (HRegion)GetLinkValue(MeasureRegionLinkText);
                if (region != null && region.IsInitialized())
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "green",
                        new HObject(region)));
                    ShowHRoi();
                }
                return;
            }

            if (MeasureRegionSource != eMeasureRegionSource.Manual)
                return;

            string roiName = ModuleParam.ModuleName + "MeasureROI";
            if (_RoiList == null)
                _RoiList = new Dictionary<string, ROI>();

            if (DispImage != null && !_RoiList.ContainsKey(roiName))
            {
                DispImage.GetImageSize(out int w, out int h);
                view.mWindowH.WindowH.genRect2(roiName, h / 2.0, w / 2.0, 0, w / 8.0, h / 8.0, ref _RoiList);
                TranMeasureRoi.MidC = w / 2.0;
                TranMeasureRoi.MidR = h / 2.0;
                TranMeasureRoi.Length1 = w / 8.0;
                TranMeasureRoi.Length2 = h / 8.0;
                TranMeasureRoi.Deg = 0;
            }
            else if (DispImage != null)
            {
                view.mWindowH.WindowH.genRect2(
                    roiName,
                    TranMeasureRoi.MidR,
                    TranMeasureRoi.MidC,
                    TranMeasureRoi.Phi,
                    TranMeasureRoi.Length1,
                    TranMeasureRoi.Length2,
                    ref _RoiList);
            }
        }

        private void RefreshMeasureRegionPreview(bool reloadImage = false)
        {
            var view = ModuleView as VolumeMeasurementView;
            if (view == null || view.mWindowH == null)
                return;

            if (reloadImage || DispImage == null || !DispImage.IsInitialized())
            {
                if (string.IsNullOrEmpty(InputImageLinkText))
                    return;

                GetDispImage(InputImageLinkText, true);
            }

            if (DispImage == null || !DispImage.IsInitialized())
                return;

            view.mWindowH.HobjectToHimage(DispImage);
            ClearRoiAndText();
            _RoiList?.Clear();
            view.mWindowH.ClearROI();

            if (MeasureRegionSource == eMeasureRegionSource.LinkRegion)
            {
                ShowLinkedMeasureRegion();
                return;
            }

            InitMeasureRoiMethod();
        }

        private void ShowLinkedMeasureRegion()
        {
            if (string.IsNullOrEmpty(MeasureRegionLinkText))
                return;

            HRegion region = (HRegion)GetLinkValue(MeasureRegionLinkText);
            if (region == null || !region.IsInitialized())
                return;

            ShowHRoi(new HRoi(
                ModuleParam.ModuleEncode,
                ModuleParam.ModuleName,
                ModuleParam.Remarks,
                HRoiType.检测结果,
                "green",
                new HObject(region)));
            ShowHRoi();
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            var view = ModuleView as VolumeMeasurementView;
            if (view == null || view.mWindowH == null)
                return;

            string info;
            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out info, out string index);
            if (roi is ROIRectangle2 rect2 && index == ModuleParam.ModuleName + "MeasureROI")
            {
                TranMeasureRoi.MidC = Math.Round(rect2.MidC, 3);
                TranMeasureRoi.MidR = Math.Round(rect2.MidR, 3);
                TranMeasureRoi.Length1 = Math.Round(rect2.Length1, 3);
                TranMeasureRoi.Length2 = Math.Round(rect2.Length2, 3);
                TranMeasureRoi.Deg = Math.Round(rect2.Deg, 3);
                ExeModule();
                InitMeasureRoiMethod();
            }
        }

        private double ResolveLinkValue(LinkVarModel linkVar)
        {
            if (linkVar == null)
                return 0;

            object value = GetLinkValue(linkVar);
            if (value == null)
                return 0;

            double.TryParse(value.ToString(), out double result);
            return result;
        }

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                Set(ref _InputImageLinkText, value);
                RefreshMeasureRegionPreview(true);
            }
        }

        private string _BasePlaneLinkText;
        public string BasePlaneLinkText
        {
            get { return _BasePlaneLinkText; }
            set { Set(ref _BasePlaneLinkText, value); }
        }

        private string _MeasureRegionLinkText;
        public string MeasureRegionLinkText
        {
            get { return _MeasureRegionLinkText; }
            set
            {
                Set(ref _MeasureRegionLinkText, value);
                RefreshMeasureRegionPreview();
            }
        }

        private eVolumeCalcMode _CalcMode = eVolumeCalcMode.AbovePlane;
        public eVolumeCalcMode CalcMode
        {
            get { return _CalcMode; }
            set { Set(ref _CalcMode, value); }
        }

        private eMeasureRegionSource _MeasureRegionSource = eMeasureRegionSource.FullImage;
        public eMeasureRegionSource MeasureRegionSource
        {
            get { return _MeasureRegionSource; }
            set
            {
                Set(ref _MeasureRegionSource, value);
                RefreshMeasureRegionPreview();
            }
        }

        public LinkVarModel RemoveHeight { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel ResolutionX { get; set; } = new LinkVarModel() { Text = "1" };
        public LinkVarModel ResolutionY { get; set; } = new LinkVarModel() { Text = "1" };
        public LinkVarModel ResolutionZ { get; set; } = new LinkVarModel() { Text = "1" };

        private string _VolumeUnit = "mm^3";
        public string VolumeUnit
        {
            get { return _VolumeUnit; }
            set { Set(ref _VolumeUnit, value); }
        }

        public ROIRectangle2 TranMeasureRoi = new ROIRectangle2();

        [NonSerialized]
        private Dictionary<string, ROI> _RoiList;

        private bool _ShowRegion = true;
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        private double _Volume;
        public double Volume
        {
            get { return _Volume; }
            set { Set(ref _Volume, value); }
        }

        private int _ValidPointCount;
        public int ValidPointCount
        {
            get { return _ValidPointCount; }
            set { Set(ref _ValidPointCount, value); }
        }

        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as VolumeMeasurementView;
            ClosedView = true;
            if (view.mWindowH == null)
            {
                view.mWindowH = new VMHWindowControl();
                view.winFormHost.Child = view.mWindowH;
            }

            view.mWindowH.hControl.MouseUp -= HControl_MouseUp;
            view.mWindowH.hControl.MouseUp += HControl_MouseUp;

            if (DispImage == null || !DispImage.IsInitialized())
            {
                SetDefaultLink();
                if (InputImageLinkText == null)
                    return;
            }

            GetDispImage(InputImageLinkText, true);
            if (DispImage != null && DispImage.IsInitialized())
                RefreshMeasureRegionPreview();
        }

        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                    _ExecuteCommand = new CommandBase(_ => { ExeModule(); });
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
                    _ConfirmCommand = new CommandBase(_ =>
                    {
                        var view = ModuleView as VolumeMeasurementView;
                        if (view != null && view.mWindowH != null)
                            view.mWindowH.hControl.MouseUp -= HControl_MouseUp;
                        view?.Close();
                    });
                }
                return _ConfirmCommand;
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
                case eLinkCommand.BasePlaneLink:
                    BasePlaneLinkText = obj.LinkName;
                    break;
                case eLinkCommand.MeasureRegionLink:
                    MeasureRegionLinkText = obj.LinkName;
                    break;
                case eLinkCommand.RemoveHeight:
                    RemoveHeight.Text = obj.LinkName;
                    break;
                case eLinkCommand.ResolutionX:
                    ResolutionX.Text = obj.LinkName;
                    break;
                case eLinkCommand.ResolutionY:
                    ResolutionY.Text = obj.LinkName;
                    break;
                case eLinkCommand.ResolutionZ:
                    ResolutionZ.Text = obj.LinkName;
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
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase(obj =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                break;
                            case eLinkCommand.BasePlaneLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                break;
                            case eLinkCommand.MeasureRegionLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                break;
                            default:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                break;
                        }

                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},{linkCommand}");
                    });
                }
                return _LinkCommand;
            }
        }

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["BasePlaneLinkText"] = BasePlaneLinkText ?? "";
            obj["MeasureRegionLinkText"] = MeasureRegionLinkText ?? "";
            obj["CalcMode"] = (int)CalcMode;
            obj["MeasureRegionSource"] = (int)MeasureRegionSource;
            obj["RemoveHeight"] = RemoveHeight?.Text ?? "";
            obj["ResolutionX"] = ResolutionX?.Text ?? "";
            obj["ResolutionY"] = ResolutionY?.Text ?? "";
            obj["ResolutionZ"] = ResolutionZ?.Text ?? "";
            obj["VolumeUnit"] = VolumeUnit ?? "";
            obj["ShowRegion"] = ShowRegion;
            obj["RoiMidC"] = TranMeasureRoi.MidC;
            obj["RoiMidR"] = TranMeasureRoi.MidR;
            obj["RoiLength1"] = TranMeasureRoi.Length1;
            obj["RoiLength2"] = TranMeasureRoi.Length2;
            obj["RoiDeg"] = TranMeasureRoi.Deg;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["BasePlaneLinkText"] != null) BasePlaneLinkText = obj["BasePlaneLinkText"].ToString();
                if (obj["MeasureRegionLinkText"] != null) MeasureRegionLinkText = obj["MeasureRegionLinkText"].ToString();
                if (obj["CalcMode"] != null) CalcMode = (eVolumeCalcMode)obj["CalcMode"].Value<int>();
                if (obj["MeasureRegionSource"] != null) MeasureRegionSource = (eMeasureRegionSource)obj["MeasureRegionSource"].Value<int>();
                if (obj["RemoveHeight"] != null && RemoveHeight != null) RemoveHeight.Text = obj["RemoveHeight"].ToString();
                if (obj["ResolutionX"] != null && ResolutionX != null) ResolutionX.Text = obj["ResolutionX"].ToString();
                if (obj["ResolutionY"] != null && ResolutionY != null) ResolutionY.Text = obj["ResolutionY"].ToString();
                if (obj["ResolutionZ"] != null && ResolutionZ != null) ResolutionZ.Text = obj["ResolutionZ"].ToString();
                if (obj["VolumeUnit"] != null) VolumeUnit = obj["VolumeUnit"].ToString();
                if (obj["ShowRegion"] != null) ShowRegion = obj["ShowRegion"].Value<bool>();
                if (obj["RoiMidC"] != null) TranMeasureRoi.MidC = obj["RoiMidC"].Value<double>();
                if (obj["RoiMidR"] != null) TranMeasureRoi.MidR = obj["RoiMidR"].Value<double>();
                if (obj["RoiLength1"] != null) TranMeasureRoi.Length1 = obj["RoiLength1"].Value<double>();
                if (obj["RoiLength2"] != null) TranMeasureRoi.Length2 = obj["RoiLength2"].Value<double>();
                if (obj["RoiDeg"] != null) TranMeasureRoi.Deg = obj["RoiDeg"].Value<double>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"VolumeMeasurementViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
    }
}
