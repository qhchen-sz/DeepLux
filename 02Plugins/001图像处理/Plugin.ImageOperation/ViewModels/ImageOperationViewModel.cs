using EventMgrLib;
using HalconDotNet;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.ViewModels;
using Newtonsoft.Json.Linq;
using Plugin.ImageOperation.Views;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;

namespace Plugin.ImageOperation.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        InputRoiLink,
        OperandImageLink,
    }

    public enum eOperandMode
    {
        Constant,
        Image,
    }

    public enum eRoiType
    {
        FullImage,
        RoiLink,
    }

    public enum eImageOperationType
    {
        [EnumDescription("加")]
        Add,
        [EnumDescription("减")]
        Subtract,
        [EnumDescription("乘")]
        Multiply,
        [EnumDescription("除")]
        Divide,
        [EnumDescription("开方")]
        Sqrt,
        [EnumDescription("与")]
        And,
        [EnumDescription("或")]
        Or,
        [EnumDescription("非")]
        Not,
        [EnumDescription("或非")]
        OrNot,
        [EnumDescription("与非")]
        AndNot,
    }

    [Category("图像处理")]
    [DisplayName("图像操作")]
    [ModuleImageName("ImageOperation")]
    [Serializable]
    public class ImageOperationViewModel : ModuleBase
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
            HImage sourceImage = null;
            HRegion roiRegion = null;
            HImage operandImage = null;
            HImage resultImage = null;

            try
            {
                ClearRoiAndText();
                if (string.IsNullOrWhiteSpace(InputImageLinkText))
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
                roiRegion = GetEffectiveRegion(sourceImage);

                if (OperandMode == eOperandMode.Constant)
                {
                    resultImage = ExecuteConstantOperation(sourceImage, roiRegion);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(OperandImageLinkText))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    GetDispImage(OperandImageLinkText, true);
                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    operandImage = PrepareOperandImage(new HImage(DispImage), sourceImage, roiRegion);
                    resultImage = ExecuteImageOperation(sourceImage, roiRegion, operandImage);
                }

                if (resultImage == null || !resultImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                ResultImage = new RImage(resultImage);
                DispImage = ResultImage;
                RefreshPreview();
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
                roiRegion?.Dispose();
                operandImage?.Dispose();
                resultImage?.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            if(Prj != null)
            {
                Prj.ClearOutputParam(ModuleParam);
                AddOutputParam("图像", "HImage", ResultImage);
                AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
                AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
            }
        }

        private HRegion GetEffectiveRegion(HImage sourceImage)
        {
            if (SelectedROIType == eRoiType.RoiLink && !string.IsNullOrWhiteSpace(InputRoiLinkText))
            {
                var region = ConvertToRegion(GetLinkValue(InputRoiLinkText));
                if (region != null && region.IsInitialized())
                {
                    return region;
                }
            }

            return sourceImage.GetDomain();
        }

        private HRegion ConvertToRegion(object roiObj)
        {
            try
            {
                if (roiObj == null)
                    return null;

                if (roiObj is HRegion region && region.IsInitialized())
                    return new HRegion(region);

                if (roiObj is HObject hObject && hObject.IsInitialized())
                    return new HRegion(hObject);

                if (roiObj is ROILine roiLine)
                    return new HRegion(roiLine.GetRegion());

                if (roiObj is ROICircle roiCircle)
                    return new HRegion(roiCircle.GetRegion());

                if (roiObj is ROICircularArc roiArc)
                    return new HRegion(roiArc.GetRegion());

                if (roiObj is ROIRectangle1 roiRect1)
                    return new HRegion(roiRect1.GetRegion());

                if (roiObj is ROIRectangle2 roiRect2)
                    return new HRegion(roiRect2.GetRegion());
            }
            catch
            {
            }

            return null;
        }

        private HImage ExecuteConstantOperation(HImage sourceImage, HRegion roiRegion)
        {
            HImage targetImage = null;
            HImage operatedImage = null;
            try
            {
                targetImage = SelectedROIType == eRoiType.RoiLink ? sourceImage.ReduceDomain(roiRegion) : sourceImage.CopyImage();

                switch (SelectedOperation)
                {
                    case eImageOperationType.Add:
                        operatedImage = targetImage.ScaleImage(1.0, ConstantValue);
                        break;
                    case eImageOperationType.Subtract:
                        operatedImage = targetImage.ScaleImage(1.0, -ConstantValue);
                        break;
                    case eImageOperationType.Multiply:
                        operatedImage = targetImage.ScaleImage(ConstantValue, 0.0);
                        break;
                    case eImageOperationType.Divide:
                        if (Math.Abs(ConstantValue) < 1e-9)
                        {
                            throw new InvalidOperationException("除法常量不能为0");
                        }
                        operatedImage = targetImage.ScaleImage(1.0 / ConstantValue, 0.0);
                        break;
                    case eImageOperationType.Sqrt:
                        HOperatorSet.SqrtImage(targetImage, out HObject sqrtObj);
                        operatedImage = new HImage(sqrtObj);
                        sqrtObj.Dispose();
                        break;
                    case eImageOperationType.Not:
                        operatedImage = targetImage.BitNot();
                        break;
                    default:
                        throw new InvalidOperationException("当前操作需要图像类型的操作对象");
                }

                if (SelectedROIType == eRoiType.RoiLink)
                {
                    var mergedImage = operatedImage.PaintGray(sourceImage);
                    operatedImage.Dispose();
                    operatedImage = mergedImage;
                }

                return operatedImage.CopyImage();
            }
            finally
            {
                targetImage?.Dispose();
                operatedImage?.Dispose();
            }
        }

        private HImage ExecuteImageOperation(HImage sourceImage, HRegion roiRegion, HImage operandImage)
        {
            HImage targetImage = null;
            HImage targetOperandImage = null;
            HImage operatedImage = null;
            try
            {
                if (SelectedROIType == eRoiType.RoiLink)
                {
                    targetImage = sourceImage.ReduceDomain(roiRegion);
                    targetOperandImage = operandImage.ReduceDomain(roiRegion);
                }
                else
                {
                    targetImage = sourceImage.CopyImage();
                    targetOperandImage = operandImage.CopyImage();
                }

                switch (SelectedOperation)
                {
                    case eImageOperationType.Add:
                        operatedImage = targetImage.AddImage(targetOperandImage, MultFactor, AddFactor);
                        break;
                    case eImageOperationType.Subtract:
                        operatedImage = targetImage.SubImage(targetOperandImage, MultFactor, AddFactor);
                        break;
                    case eImageOperationType.Multiply:
                        operatedImage = targetImage.MultImage(targetOperandImage, MultFactor, AddFactor);
                        break;
                    case eImageOperationType.Divide:
                        operatedImage = targetImage.DivImage(targetOperandImage, MultFactor, AddFactor);
                        break;
                    case eImageOperationType.And:
                        operatedImage = targetImage.BitAnd(targetOperandImage);
                        break;
                    case eImageOperationType.Or:
                        operatedImage = targetImage.BitOr(targetOperandImage);
                        break;
                    case eImageOperationType.Not:
                        operatedImage = targetImage.BitNot();
                        break;
                    case eImageOperationType.OrNot:
                        using (var bitNot = targetOperandImage.BitNot())
                        {
                            operatedImage = targetImage.BitOr(bitNot);
                        }
                        break;
                    case eImageOperationType.AndNot:
                        using (var bitNot = targetOperandImage.BitNot())
                        {
                            operatedImage = targetImage.BitAnd(bitNot);
                        }
                        break;
                    case eImageOperationType.Sqrt:
                        HOperatorSet.SqrtImage(targetImage, out HObject sqrtObj);
                        operatedImage = new HImage(sqrtObj);
                        sqrtObj.Dispose();
                        break;
                    default:
                        throw new InvalidOperationException("不支持的图像操作");
                }

                if (SelectedROIType == eRoiType.RoiLink)
                {
                    var mergedImage = operatedImage.PaintGray(sourceImage);
                    operatedImage.Dispose();
                    operatedImage = mergedImage;
                }

                return operatedImage.CopyImage();
            }
            finally
            {
                targetImage?.Dispose();
                targetOperandImage?.Dispose();
                operatedImage?.Dispose();
            }
        }

        private HImage PrepareOperandImage(HImage operandImage, HImage sourceImage, HRegion roiRegion)
        {
            HImage currentImage = null;
            HImage normalizedImage = null;
            try
            {
                currentImage = operandImage.CopyImage();

                if (Math.Abs(OffsetAngle) > 1e-9)
                {
                    var nextImage = currentImage.RotateImage(OffsetAngle, "constant");
                    currentImage.Dispose();
                    currentImage = nextImage;
                }

                if (Math.Abs(OffsetX) > 1e-9 || Math.Abs(OffsetY) > 1e-9)
                {
                    HHomMat2D matrix = new HHomMat2D();
                    matrix = matrix.HomMat2dTranslate(OffsetY, OffsetX);
                    var nextImage = currentImage.AffineTransImage(matrix, "constant", "false");
                    currentImage.Dispose();
                    currentImage = nextImage;
                }

                HOperatorSet.GetImageSize(sourceImage, out HTuple sourceWidth, out HTuple sourceHeight);
                HOperatorSet.GetImageSize(currentImage, out HTuple operandWidth, out HTuple operandHeight);
                if (sourceWidth.I != operandWidth.I || sourceHeight.I != operandHeight.I)
                {
                    HOperatorSet.ZoomImageSize(currentImage, out HObject resizedObj, sourceWidth, sourceHeight, "constant");
                    normalizedImage = new HImage(resizedObj);
                    resizedObj.Dispose();
                    currentImage.Dispose();
                    currentImage = normalizedImage;
                    normalizedImage = null;
                }

                if (SelectedROIType == eRoiType.RoiLink)
                {
                    var roiImage = currentImage.ReduceDomain(roiRegion);
                    currentImage.Dispose();
                    currentImage = roiImage;
                }

                return currentImage.CopyImage();
            }
            finally
            {
                currentImage?.Dispose();
                normalizedImage?.Dispose();
            }
        }

        private void ShowCurrentRoi()
        {
            if (SelectedROIType != eRoiType.RoiLink || string.IsNullOrWhiteSpace(InputRoiLinkText))
            {
                ShowHRoi();
                return;
            }

            var region = ConvertToRegion(GetLinkValue(InputRoiLinkText));
            if (region != null && region.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                ShowHRoi();
                region.Dispose();
            }
            else
            {
                ShowHRoi();
            }
        }

        private void RefreshPreview()
        {
            var view = ModuleView as ImageOperationView;
            if (view?.mWindowH == null)
                return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.mWindowH.Image = new RImage(DispImage);
                    view.mWindowH.DispObj(view.mWindowH.Image);
                }
                else
                {
                    view.mWindowH.ClearWindow();
                }
            });
        }

        private void TryExecutePreview()
        {
            if (string.IsNullOrWhiteSpace(InputImageLinkText))
                return;

            if (!IsRealtimePreview)
            {
                GetDispImage(InputImageLinkText, true);
                RefreshPreview();
                ShowCurrentRoi();
                return;
            }

            try
            {
                ExeModule();
                ShowCurrentRoi();
            }
            catch
            {
            }
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
                if (!string.IsNullOrWhiteSpace(_InputImageLinkText))
                {
                    GetDispImage(_InputImageLinkText, true);
                    RefreshPreview();
                    ShowCurrentRoi();
                    TryExecutePreview();
                }
            }
        }

        private eRoiType _SelectedROIType = eRoiType.FullImage;
        public eRoiType SelectedROIType
        {
            get { return _SelectedROIType; }
            set
            {
                Set(ref _SelectedROIType, value);
                ShowCurrentRoi();
                TryExecutePreview();
            }
        }

        private string _InputRoiLinkText;
        public string InputRoiLinkText
        {
            get { return _InputRoiLinkText; }
            set
            {
                Set(ref _InputRoiLinkText, value);
                ShowCurrentRoi();
                TryExecutePreview();
            }
        }

        private string _OperandImageLinkText;
        public string OperandImageLinkText
        {
            get { return _OperandImageLinkText; }
            set
            {
                _OperandImageLinkText = value;
                RaisePropertyChanged();
                TryExecutePreview();
            }
        }

        private eImageOperationType _SelectedOperation = eImageOperationType.Add;
        public eImageOperationType SelectedOperation
        {
            get { return _SelectedOperation; }
            set
            {
                Set(ref _SelectedOperation, value);
                TryExecutePreview();
            }
        }

        private eOperandMode _OperandMode = eOperandMode.Constant;
        public eOperandMode OperandMode
        {
            get { return _OperandMode; }
            set
            {
                Set(ref _OperandMode, value);
                TryExecutePreview();
            }
        }

        private bool _IsRealtimePreview = true;
        public bool IsRealtimePreview
        {
            get { return _IsRealtimePreview; }
            set
            {
                Set(ref _IsRealtimePreview, value);
                if (_IsRealtimePreview)
                {
                    TryExecutePreview();
                }
                else if (!string.IsNullOrWhiteSpace(InputImageLinkText))
                {
                    GetDispImage(InputImageLinkText, true);
                    RefreshPreview();
                    ShowCurrentRoi();
                }
            }
        }

        private double _ConstantValue = 100;
        public double ConstantValue
        {
            get { return _ConstantValue; }
            set
            {
                Set(ref _ConstantValue, value);
                TryExecutePreview();
            }
        }

        private double _OffsetX;
        public double OffsetX
        {
            get { return _OffsetX; }
            set
            {
                Set(ref _OffsetX, value);
                TryExecutePreview();
            }
        }

        private double _OffsetY;
        public double OffsetY
        {
            get { return _OffsetY; }
            set
            {
                Set(ref _OffsetY, value);
                TryExecutePreview();
            }
        }

        private double _OffsetAngle;
        public double OffsetAngle
        {
            get { return _OffsetAngle; }
            set
            {
                Set(ref _OffsetAngle, value);
                TryExecutePreview();
            }
        }

        private double _MultFactor = 1.0;
        public double MultFactor
        {
            get { return _MultFactor; }
            set
            {
                Set(ref _MultFactor, value);
                TryExecutePreview();
            }
        }

        private double _AddFactor;
        public double AddFactor
        {
            get { return _AddFactor; }
            set
            {
                Set(ref _AddFactor, value);
                TryExecutePreview();
            }
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
            var view = ModuleView as ImageOperationView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                SetDefaultLink();
                ShowCurrentRoi();
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
                case eLinkCommand.InputRoiLink:
                    InputRoiLinkText = obj.LinkName;
                    break;
                case eLinkCommand.OperandImageLink:
                    OperandImageLinkText = obj.LinkName;
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
                        string type = obj.ToString() == nameof(eLinkCommand.InputRoiLink) ? "HRegion" : "HImage";
                        CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, type);
                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},{obj}");
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
                        var view = ModuleView as ImageOperationView;
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
            obj["SelectedROIType"] = (int)SelectedROIType;
            obj["InputRoiLinkText"] = InputRoiLinkText ?? "";
            obj["OperandImageLinkText"] = OperandImageLinkText ?? "";
            obj["SelectedOperation"] = (int)SelectedOperation;
            obj["OperandMode"] = (int)OperandMode;
            obj["IsRealtimePreview"] = IsRealtimePreview;
            obj["ConstantValue"] = ConstantValue;
            obj["OffsetX"] = OffsetX;
            obj["OffsetY"] = OffsetY;
            obj["OffsetAngle"] = OffsetAngle;
            obj["MultFactor"] = MultFactor;
            obj["AddFactor"] = AddFactor;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLinkText"] != null)
                {
                    InputImageLinkText = obj["InputImageLinkText"].ToString();
                }
                if (obj["SelectedROIType"] != null) SelectedROIType = (eRoiType)obj["SelectedROIType"].Value<int>();
                if (obj["InputRoiLinkText"] != null) InputRoiLinkText = obj["InputRoiLinkText"].ToString();
                if (obj["OperandImageLinkText"] != null) OperandImageLinkText = obj["OperandImageLinkText"].ToString();
                if (obj["SelectedOperation"] != null) SelectedOperation = (eImageOperationType)obj["SelectedOperation"].Value<int>();
                if (obj["OperandMode"] != null) OperandMode = (eOperandMode)obj["OperandMode"].Value<int>();
                if (obj["IsRealtimePreview"] != null) IsRealtimePreview = obj["IsRealtimePreview"].Value<bool>();
                if (obj["ConstantValue"] != null) ConstantValue = obj["ConstantValue"].Value<double>();
                if (obj["OffsetX"] != null) OffsetX = obj["OffsetX"].Value<double>();
                if (obj["OffsetY"] != null) OffsetY = obj["OffsetY"].Value<double>();
                if (obj["OffsetAngle"] != null) OffsetAngle = obj["OffsetAngle"].Value<double>();
                if (obj["MultFactor"] != null) MultFactor = obj["MultFactor"].Value<double>();
                if (obj["AddFactor"] != null) AddFactor = obj["AddFactor"].Value<double>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"ImageOperationViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
