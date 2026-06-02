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
using Plugin.DepthToImage.Views;
using System;
using System.ComponentModel;
using System.Linq;
using VM.Halcon;
using VM.Halcon.Config;

namespace Plugin.DepthToImage.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        PlaneImageLink,
    }

    [Category("3D")]
    [DisplayName("FS深度转图片")]
    [ModuleImageName("DepthToImage")]
    [Serializable]
    public class DepthToImageViewModel : ModuleBase
    {
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
            HRegion domain = null;
            HImage sourceImage = null;
            HObject depthChannelObj = null;
            HObject depthRealObj = null;
            HImage depthImage = null;
            HObject physicalHeightObj = null;
            HImage physicalHeightImage = null;
            HImage workingImage = null;
            HImage outputImage = null;
            bool outputAssigned = false;

            try
            {
                ClearRoiAndText();
                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                if (string.IsNullOrEmpty(PlaneImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("未链接基准平面图像"));
                    return false;
                }

                if (ResolutionZ <= 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("Z分辨率必须大于0"));
                    return false;
                }

                // === 获取深度图像 ===
                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                sourceImage = new HImage(DispImage);

                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (channelCount.I < 1)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 取高度通道（与3D控件保持一致）
                if (channelCount == 2)
                    HOperatorSet.Decompose2(sourceImage, out depthChannelObj, out HObject _);
                else
                    HOperatorSet.AccessChannel(sourceImage, out depthChannelObj, 1);
                HOperatorSet.ConvertImageType(depthChannelObj, out depthRealObj, "real");
                depthImage = new HImage(depthRealObj);
                HOperatorSet.GetImageSize(depthImage, out HTuple width, out HTuple height);

                SourceWidth = width.I;
                SourceHeight = height.I;

                // 深度图 × Z分辨率 → 物理高度
                HOperatorSet.ScaleImage(depthImage, out physicalHeightObj, ResolutionZ, 0.0);
                physicalHeightImage = new HImage(physicalHeightObj);
                physicalHeightObj.Dispose();
                physicalHeightObj = null;

                // === 获取基准平面图像（来自上游 FitPlane 模块）===
                var savedDisp = DispImage;
                GetDispImage(PlaneImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    DispImage = savedDisp;
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("基准平面图像无效"));
                    return false;
                }

                HImage planeImage;
                using (HImage tmpImg = new HImage(DispImage))
                {
                    HOperatorSet.CountChannels(tmpImg, out HTuple pc);
                    HObject planeChObj;
                    if (pc == 2)
                        HOperatorSet.Decompose2(tmpImg, out planeChObj, out HObject _);
                    else
                        HOperatorSet.AccessChannel(tmpImg, out planeChObj, 1);
                    HOperatorSet.ConvertImageType(planeChObj, out HObject planeRealObj, "real");
                    planeImage = new HImage(planeRealObj);
                    planeRealObj.Dispose();
                    planeChObj.Dispose();
                }
                DispImage = savedDisp;
                if (ModuleView != null && ModuleView.mWindowH != null)
                    ModuleView.mWindowH.HobjectToHimage(DispImage);

                // 基准平面也按相同 Z 分辨率缩放，与深度图单位一致
                HOperatorSet.ScaleImage(planeImage, out HObject planeScaledObj, ResolutionZ, 0.0);
                planeImage.Dispose();
                planeImage = new HImage(planeScaledObj);
                planeScaledObj.Dispose();

                // 对基准平面施加平移（移动基准面）
                if (Math.Abs(TranslateSize) > 1e-12)
                {
                    HOperatorSet.ScaleImage(planeImage, out HObject shiftedPlane, 1.0, TranslateSize);
                    planeImage.Dispose();
                    planeImage = new HImage(shiftedPlane);
                    shiftedPlane.Dispose();
                }

                domain = physicalHeightImage.GetDomain();

                // 减平面 → 残差高度
                HOperatorSet.SubImage(physicalHeightImage, planeImage, out HObject residualObj, 1.0, 0.0);
                workingImage = new HImage(residualObj);
                residualObj.Dispose();
                planeImage.Dispose();

                // 统计残差范围
                HOperatorSet.MinMaxGray(domain, workingImage, 0.0, out HTuple actualMinZ, out HTuple actualMaxZ, out _);

                if (ConvertMaxZ <= ConvertMinZ)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("转换最大Z值必须大于转换最小Z值"));
                    return false;
                }

                // 阈值二值化：范围内→255（白），范围外→0（黑）
                HOperatorSet.Threshold(workingImage, out HObject keepRegion, ConvertMinZ, ConvertMaxZ);
                HOperatorSet.GetImageSize(workingImage, out HTuple imgW, out HTuple imgH);
                HOperatorSet.RegionToBin(keepRegion, out HObject binImage, 255, 0, imgW, imgH);
                outputImage = new HImage(binImage);
                binImage.Dispose();
                keepRegion.Dispose();

                ResultImage = new RImage(outputImage);
                outputAssigned = true;
                DispImage = ResultImage;

                ResultMinZ = actualMinZ.D;
                ResultMaxZ = actualMaxZ.D;
                ResultScale = 0;

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
                domain?.Dispose();
                sourceImage?.Dispose();
                depthChannelObj?.Dispose();
                depthRealObj?.Dispose();
                depthImage?.Dispose();
                physicalHeightObj?.Dispose();
                physicalHeightImage?.Dispose();
                workingImage?.Dispose();
                if (!outputAssigned)
                    outputImage?.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            Prj.ClearOutputParam(ModuleParam);
            AddOutputParam("结果图像", "HImage", ResultImage);
            AddOutputParam("最小Z", "double", ResultMinZ);
            AddOutputParam("最大Z", "double", ResultMaxZ);
            AddOutputParam("映射比例", "double", ResultScale);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText, true);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    ShowHRoi();
                }
            }
        }

        private string _PlaneImageLinkText;
        public string PlaneImageLinkText
        {
            get { return _PlaneImageLinkText; }
            set { Set(ref _PlaneImageLinkText, value); }
        }

        private double _ResolutionZ = 1.0;
        public double ResolutionZ
        {
            get { return _ResolutionZ; }
            set { Set(ref _ResolutionZ, value); }
        }

        private double _ConvertMinZ = -10.0;
        public double ConvertMinZ
        {
            get { return _ConvertMinZ; }
            set { Set(ref _ConvertMinZ, value); }
        }

        private double _ConvertMaxZ = 10.0;
        public double ConvertMaxZ
        {
            get { return _ConvertMaxZ; }
            set { Set(ref _ConvertMaxZ, value); }
        }

        private double _TranslateSize = 0.001;
        public double TranslateSize
        {
            get { return _TranslateSize; }
            set { Set(ref _TranslateSize, value); }
        }

        private int _SourceWidth;
        public int SourceWidth
        {
            get { return _SourceWidth; }
            set { Set(ref _SourceWidth, value); }
        }

        private int _SourceHeight;
        public int SourceHeight
        {
            get { return _SourceHeight; }
            set { Set(ref _SourceHeight, value); }
        }

        private double _ResultMinZ;
        public double ResultMinZ
        {
            get { return _ResultMinZ; }
            set { Set(ref _ResultMinZ, value); }
        }

        private double _ResultMaxZ;
        public double ResultMaxZ
        {
            get { return _ResultMaxZ; }
            set { Set(ref _ResultMaxZ, value); }
        }

        private double _ResultScale;
        public double ResultScale
        {
            get { return _ResultScale; }
            set { Set(ref _ResultScale, value); }
        }

        [NonSerialized]
        private RImage _ResultImage;
        public RImage ResultImage
        {
            get { return _ResultImage; }
            set { Set(ref _ResultImage, value); }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as DepthToImageView;
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
                case eLinkCommand.PlaneImageLink:
                    PlaneImageLinkText = obj.LinkName;
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
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.PlaneImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},PlaneImageLink");
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
                {
                    _ExecuteCommand = new CommandBase((obj) => { ExeModule(); });
                }
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
                        var view = ModuleView as DepthToImageView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["PlaneImageLinkText"] = PlaneImageLinkText ?? "";
            obj["ResolutionZ"] = ResolutionZ;
            obj["ConvertMinZ"] = ConvertMinZ;
            obj["ConvertMaxZ"] = ConvertMaxZ;
            obj["TranslateSize"] = TranslateSize;
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
                if (obj["PlaneImageLinkText"] != null) PlaneImageLinkText = obj["PlaneImageLinkText"].ToString();
                if (obj["ResolutionZ"] != null) ResolutionZ = obj["ResolutionZ"].Value<double>();
                if (obj["ConvertMinZ"] != null) ConvertMinZ = obj["ConvertMinZ"].Value<double>();
                if (obj["ConvertMaxZ"] != null) ConvertMaxZ = obj["ConvertMaxZ"].Value<double>();
                if (obj["TranslateSize"] != null) TranslateSize = obj["TranslateSize"].Value<double>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"DepthToImageViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
