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
    }

    public enum eFitType
    {
        [EnumDescription("全部")]
        All,
        [EnumDescription("下陷")]
        Dent,
        [EnumDescription("凸起")]
        Bump,
    }

    [Category("3D")]
    [DisplayName("FS深度转图片")]
    [ModuleImageName("DepthToImage")]
    [Serializable]
    public class DepthToImageViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            if (InputImageLinkText != null) return;

            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
            {
                return;
            }

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
            HImage leveledImage = null;
            HObject planeObj = null;
            HImage mappedImage = null;
            HImage outputImage = null;

            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

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

                HOperatorSet.AccessChannel(sourceImage, out depthChannelObj, 1);
                HOperatorSet.ConvertImageType(depthChannelObj, out depthRealObj, "real");
                depthImage = new HImage(depthRealObj);
                HOperatorSet.GetImageSize(depthImage, out HTuple width, out HTuple height);

                SourceWidth = width.I;
                SourceHeight = height.I;

                domain = depthImage.GetDomain();
                HOperatorSet.FitSurfaceFirstOrder(
                    domain,
                    depthImage,
                    "regression",
                    Math.Max(1, FitParamX),
                    0.1,
                    out HTuple alpha,
                    out HTuple beta,
                    out HTuple gamma
                );

                HTuple refRow;
                HTuple refCol;
                if (ReferenceZ >= 0)
                {
                    refRow = height.D / 2.0;
                    refCol = width.D / 2.0;
                }
                else
                {
                    HOperatorSet.AreaCenter(domain, out _, out refRow, out refCol);
                }

                double gammaAdj = gamma.D + ReferenceZ;
                HOperatorSet.GenImageSurfaceFirstOrder(
                    out planeObj,
                    "real",
                    alpha,
                    beta,
                    gammaAdj,
                    refRow,
                    refCol,
                    width,
                    height
                );

                HOperatorSet.SubImage(depthImage, planeObj, out HObject leveledObj, 1.0, 0.0);
                leveledImage = new HImage(leveledObj);
                leveledObj.Dispose();

                HImage workingImage = leveledImage;

                if (HeightThreshold > 0)
                {
                    double threshold = Math.Abs(HeightThreshold);
                    switch (FitType)
                    {
                        case eFitType.All:
                            HOperatorSet.Threshold(workingImage, out HObject noiseRegion, -threshold, threshold);
                            HOperatorSet.PaintRegion(noiseRegion, workingImage, out HObject allObj, 0, "fill");
                            noiseRegion.Dispose();
                            leveledImage.Dispose();
                            leveledImage = new HImage(allObj);
                            allObj.Dispose();
                            workingImage = leveledImage;
                            break;
                        case eFitType.Dent:
                            HOperatorSet.Threshold(workingImage, out HObject dentRegion, "min", -threshold);
                            HOperatorSet.Difference(domain, dentRegion, out HObject dentClearRegion);
                            HOperatorSet.PaintRegion(dentClearRegion, workingImage, out HObject dentObj, 0, "fill");
                            dentRegion.Dispose();
                            dentClearRegion.Dispose();
                            leveledImage.Dispose();
                            leveledImage = new HImage(dentObj);
                            dentObj.Dispose();
                            workingImage = leveledImage;
                            break;
                        case eFitType.Bump:
                            HOperatorSet.Threshold(workingImage, out HObject bumpRegion, threshold, "max");
                            HOperatorSet.Difference(domain, bumpRegion, out HObject bumpClearRegion);
                            HOperatorSet.PaintRegion(bumpClearRegion, workingImage, out HObject bumpObj, 0, "fill");
                            bumpRegion.Dispose();
                            bumpClearRegion.Dispose();
                            leveledImage.Dispose();
                            leveledImage = new HImage(bumpObj);
                            bumpObj.Dispose();
                            workingImage = leveledImage;
                            break;
                    }
                }

                double minZ = ConvertMinZ;
                double maxZ = ConvertMaxZ;
                if (maxZ <= minZ)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("转换最大Z值必须大于转换最小Z值"));
                    return false;
                }

                double scale = 255.0 / (maxZ - minZ);
                double offset = -minZ * scale;
                HOperatorSet.ScaleImage(workingImage, out HObject scaledObj, scale, offset);
                mappedImage = new HImage(scaledObj);
                scaledObj.Dispose();

                HOperatorSet.Threshold(mappedImage, out HObject lowRegion, "min", 0);
                HOperatorSet.PaintRegion(lowRegion, mappedImage, out HObject clippedLowObj, 0, "fill");
                lowRegion.Dispose();
                mappedImage.Dispose();
                mappedImage = new HImage(clippedLowObj);
                clippedLowObj.Dispose();

                HOperatorSet.Threshold(mappedImage, out HObject highRegion, 255, "max");
                HOperatorSet.PaintRegion(highRegion, mappedImage, out HObject clippedHighObj, 255, "fill");
                highRegion.Dispose();
                mappedImage.Dispose();
                mappedImage = new HImage(clippedHighObj);
                clippedHighObj.Dispose();

                HOperatorSet.ConvertImageType(mappedImage, out HObject byteObj, "byte");
                outputImage = new HImage(byteObj);
                byteObj.Dispose();

                if (Math.Abs(MapResolution - 1.0) > 1e-9)
                {
                    double zoomFactor = Math.Max(0.01, MapResolution);
                    HOperatorSet.ZoomImageFactor(outputImage, out HObject zoomObj, zoomFactor, zoomFactor, "nearest_neighbor");
                    outputImage.Dispose();
                    outputImage = new HImage(zoomObj);
                    zoomObj.Dispose();
                }

                ResultImage = new RImage(outputImage);
                DispImage = ResultImage;

                ResultMinZ = minZ;
                ResultMaxZ = maxZ;
                ResultScale = scale;

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
                leveledImage?.Dispose();
                planeObj?.Dispose();
                mappedImage?.Dispose();
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

        private double _ResolutionX = 0.01;
        public double ResolutionX
        {
            get { return _ResolutionX; }
            set { Set(ref _ResolutionX, value); }
        }

        private double _ResolutionY = 0.01;
        public double ResolutionY
        {
            get { return _ResolutionY; }
            set { Set(ref _ResolutionY, value); }
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

        private double _MapResolution = 0.05;
        public double MapResolution
        {
            get { return _MapResolution; }
            set { Set(ref _MapResolution, value); }
        }

        private double _HeightThreshold = 1.0;
        public double HeightThreshold
        {
            get { return _HeightThreshold; }
            set { Set(ref _HeightThreshold, value); }
        }

        private double _TranslateSize = 0.001;
        public double TranslateSize
        {
            get { return _TranslateSize; }
            set { Set(ref _TranslateSize, value); }
        }

        private eFitType _FitType = eFitType.All;
        public eFitType FitType
        {
            get { return _FitType; }
            set { Set(ref _FitType, value); }
        }

        private int _FitParamX = 20;
        public int FitParamX
        {
            get { return _FitParamX; }
            set { Set(ref _FitParamX, value); }
        }

        private int _FitParamY = 20;
        public int FitParamY
        {
            get { return _FitParamY; }
            set { Set(ref _FitParamY, value); }
        }

        private double _ReferenceZ = -1.0;
        public double ReferenceZ
        {
            get { return _ReferenceZ; }
            set { Set(ref _ReferenceZ, value); }
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
            if (linkCommand == eLinkCommand.InputImageLink)
            {
                InputImageLinkText = obj.LinkName;
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
            obj["ResolutionX"] = ResolutionX;
            obj["ResolutionY"] = ResolutionY;
            obj["ResolutionZ"] = ResolutionZ;
            obj["ConvertMinZ"] = ConvertMinZ;
            obj["ConvertMaxZ"] = ConvertMaxZ;
            obj["MapResolution"] = MapResolution;
            obj["HeightThreshold"] = HeightThreshold;
            obj["TranslateSize"] = TranslateSize;
            obj["FitType"] = (int)FitType;
            obj["FitParamX"] = FitParamX;
            obj["FitParamY"] = FitParamY;
            obj["ReferenceZ"] = ReferenceZ;
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
                if (obj["ResolutionX"] != null) ResolutionX = obj["ResolutionX"].Value<double>();
                if (obj["ResolutionY"] != null) ResolutionY = obj["ResolutionY"].Value<double>();
                if (obj["ResolutionZ"] != null) ResolutionZ = obj["ResolutionZ"].Value<double>();
                if (obj["ConvertMinZ"] != null) ConvertMinZ = obj["ConvertMinZ"].Value<double>();
                if (obj["ConvertMaxZ"] != null) ConvertMaxZ = obj["ConvertMaxZ"].Value<double>();
                if (obj["MapResolution"] != null) MapResolution = obj["MapResolution"].Value<double>();
                if (obj["HeightThreshold"] != null) HeightThreshold = obj["HeightThreshold"].Value<double>();
                if (obj["TranslateSize"] != null) TranslateSize = obj["TranslateSize"].Value<double>();
                if (obj["FitType"] != null) FitType = (eFitType)obj["FitType"].Value<int>();
                if (obj["FitParamX"] != null) FitParamX = obj["FitParamX"].Value<int>();
                if (obj["FitParamY"] != null) FitParamY = obj["FitParamY"].Value<int>();
                if (obj["ReferenceZ"] != null) ReferenceZ = obj["ReferenceZ"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"DepthToImageViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
