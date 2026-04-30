using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using HalconDotNet;
using HandyControl.Controls;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using EventMgrLib;
using Plugin.CreateROI.Views;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Views.Dock;
using HV.Dialogs.Views;

namespace Plugin.CreateROI.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Rect2Len1,
        Rect2Len2,
        Rect2MidR,
        Rect2MidC,
        Rect2Deg,
        CircleX,
        CircleY,
        CircleRadius
    }
    public enum eEditMode
    {
        正常显示,
        绘制涂抹,
        擦除涂抹,
    }
    public enum eDrawShape
    {
        矩形,
        圆形,
    }
    #endregion

    [Category("检测识别")]
    [DisplayName("创建ROI")]
    [ModuleImageName("CreateROI")]
    [Serializable]
    public class CreateROIViewModel : ModuleBase
    {
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
            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                switch (SelectedROIType)
                {
                    case eDrawShape.矩形:
                        OutRegion.GenRectangle2(
                            Convert.ToDouble(GetLinkValue(Rect2MidR)),
                            Convert.ToDouble(GetLinkValue(Rect2MidC)),
                            (double)((HTuple)(Convert.ToDouble(GetLinkValue(Rect2Deg)))).TupleRad(),
                            Convert.ToDouble(GetLinkValue(Rect2Len1)),
                            Convert.ToDouble(GetLinkValue(Rect2Len2)));
                        break;
                    case eDrawShape.圆形:
                        OutRegion.GenCircle(
                            Convert.ToDouble(GetLinkValue(CircleY)),
                            Convert.ToDouble(GetLinkValue(CircleX)),
                            Convert.ToDouble(GetLinkValue(CircleRadius)));
                        break;
                }
                if (OutRegion == null || !OutRegion.IsInitialized() || OutRegion.Area <= 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetHomMat2D();
                if (HomMat2D != null && HomMat2D.Length > 0)
                {
                    OutRegion = OutRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                    if (finalRegion != null && finalRegion.IsInitialized())
                        finalRegion_Temp = finalRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                    else
                        finalRegion_Temp = new HRegion(finalRegion);
                }
                else
                {
                    if (finalRegion != null && finalRegion.IsInitialized())
                        finalRegion_Temp = new HRegion(finalRegion);
                }

                if (finalRegion_Temp != null && finalRegion_Temp.IsInitialized())
                    OutRegion = OutRegion.Difference(finalRegion_Temp);

                var view = ModuleView as CreateROIView;
                VMHWindowControl mWindowH;
                if (view == null || view.IsClosed)
                    mWindowH = ViewDic.GetView(DispImage.DispViewID);
                else
                    mWindowH = view.mWindowH;

                if (ShowResultContour && OutRegion != null && OutRegion.IsInitialized())
                    mWindowH.WindowH.DispHobject(OutRegion, "green", false);

                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            AddOutputParam("区域", "HRegion", OutRegion);
            switch (SelectedROIType)
            {
                case eDrawShape.矩形:
                    AddOutputParam("矩形中心行", "double", Convert.ToDouble(GetLinkValue(Rect2MidR)));
                    AddOutputParam("矩形中心列", "double", Convert.ToDouble(GetLinkValue(Rect2MidC)));
                    AddOutputParam("矩形角度", "double", Convert.ToDouble(GetLinkValue(Rect2Deg)));
                    AddOutputParam("矩形半长1", "double", Convert.ToDouble(GetLinkValue(Rect2Len1)));
                    AddOutputParam("矩形半长2", "double", Convert.ToDouble(GetLinkValue(Rect2Len2)));
                    AddOutputParam("ROI类型", "string", "矩形");
                    break;
                case eDrawShape.圆形:
                    AddOutputParam("圆形中心行", "double", Convert.ToDouble(GetLinkValue(CircleX)));
                    AddOutputParam("圆形中心列", "double", Convert.ToDouble(GetLinkValue(CircleY)));
                    AddOutputParam("圆形半径", "double", Convert.ToDouble(GetLinkValue(CircleRadius)));
                    AddOutputParam("ROI类型", "string", "圆形");
                    break;
            }

            if (IsOutImageReduced && DispImage != null && DispImage.IsInitialized())
            {
                try
                {
                    if (OutRegion != null && OutRegion.IsInitialized() && OutRegion.Area > 0)
                    {
                        ImageReduced = DispImage.ReduceDomain(OutRegion);
                        if (ImageReduced != null && ImageReduced.IsInitialized())
                        {
                            HImage cropped = ImageReduced.CropDomain();
                            AddOutputParam("裁剪图像", "HImage", cropped != null && cropped.IsInitialized() ? new RImage(cropped) : new RImage());
                        }
                        else
                            AddOutputParam("裁剪图像", "HImage", new RImage());
                    }
                    else
                        AddOutputParam("裁剪图像", "HImage", new RImage());
                }
                catch (Exception ex)
                {
                    Logger.GetExceptionMsg(ex);
                    AddOutputParam("裁剪图像", "HImage", new RImage());
                }
            }

            if (IsOutImageComplement)
            {
                try
                {
                    if (OutRegion != null && OutRegion.IsInitialized() && OutRegion.Area > 0)
                    {
                        HRegion fullRegion = DispImage.GetDomain();
                        HRegion complementRegion = fullRegion.Difference(OutRegion);
                        if (complementRegion != null && complementRegion.IsInitialized() && complementRegion.Area > 0)
                        {
                            HImage complementImage = OutRegion.PaintRegion(DispImage, 0d, "fill");
                            AddOutputParam("补集图像", "HImage", complementImage != null && complementImage.IsInitialized() ? new RImage(complementImage) : new RImage());
                            complementImage?.Dispose();
                            complementRegion.Dispose();
                        }
                        else
                            AddOutputParam("补集图像", "HImage", new RImage());
                        fullRegion.Dispose();
                    }
                    else
                        AddOutputParam("补集图像", "HImage", new RImage());
                }
                catch (Exception ex)
                {
                    Logger.GetExceptionMsg(ex);
                    AddOutputParam("补集图像", "HImage", new RImage());
                }
            }
        }

        #region Prop
        [NonSerialized] private bool IsDrawing = false;
        HRegion OutRegion = new HRegion(0.0, 0, 3);
        HImage ImageReduced = new HImage(FilePaths.ConfigFilePath + "Background.bmp");

        private bool _ShowResultContour = true;
        public bool ShowResultContour { get => _ShowResultContour; set => Set(ref _ShowResultContour, value); }

        private bool _IsOutImageReduced;
        public bool IsOutImageReduced { get => _IsOutImageReduced; set => Set(ref _IsOutImageReduced, value); }

        private bool _IsOutImageComplement;
        public bool IsOutImageComplement { get => _IsOutImageComplement; set => Set(ref _IsOutImageComplement, value); }

        private string _InputImageLinkText = "";
        public string InputImageLinkText
        {
            get => _InputImageLinkText;
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                    ShowHRoi();
            }
        }

        private eDrawShape _SelectedROIType = eDrawShape.矩形;
        public eDrawShape SelectedROIType
        {
            get => _SelectedROIType;
            set => Set(ref _SelectedROIType, value, () => ShowHRoi());
        }

        private ROIRectangle2 _Rect2;
        public ROIRectangle2 Rect2
        {
            get
            {
                if (_Rect2 == null) _Rect2 = new ROIRectangle2(200, 200, 0, 30, 30);
                return _Rect2;
            }
            set => _Rect2 = value;
        }

        private ROICircle _Circle;
        public ROICircle Circle
        {
            get
            {
                if (_Circle == null) _Circle = new ROICircle(200, 200, 30);
                return _Circle;
            }
            set => _Circle = value;
        }

        private LinkVarModel _Rect2Len1 = new LinkVarModel() { Text = "30" };
        public LinkVarModel Rect2Len1 { get => _Rect2Len1; set { _Rect2Len1 = value; RaisePropertyChanged(); } }
        private LinkVarModel _Rect2Len2 = new LinkVarModel() { Text = "30" };
        public LinkVarModel Rect2Len2 { get => _Rect2Len2; set { _Rect2Len2 = value; RaisePropertyChanged(); } }
        private LinkVarModel _Rect2MidR = new LinkVarModel() { Text = "200" };
        public LinkVarModel Rect2MidR { get => _Rect2MidR; set { _Rect2MidR = value; RaisePropertyChanged(); } }
        private LinkVarModel _Rect2MidC = new LinkVarModel() { Text = "200" };
        public LinkVarModel Rect2MidC { get => _Rect2MidC; set { _Rect2MidC = value; RaisePropertyChanged(); } }
        private LinkVarModel _Rect2Deg = new LinkVarModel() { Text = "0" };
        public LinkVarModel Rect2Deg { get => _Rect2Deg; set { _Rect2Deg = value; RaisePropertyChanged(); } }
        private LinkVarModel _CircleX = new LinkVarModel() { Text = "100" };
        public LinkVarModel CircleX { get => _CircleX; set { _CircleX = value; RaisePropertyChanged(); } }
        private LinkVarModel _CircleY = new LinkVarModel() { Text = "100" };
        public LinkVarModel CircleY { get => _CircleY; set { _CircleY = value; RaisePropertyChanged(); } }
        private LinkVarModel _CircleRadius = new LinkVarModel() { Text = "30" };
        public LinkVarModel CircleRadius { get => _CircleRadius; set { _CircleRadius = value; RaisePropertyChanged(); } }

        public Array DrawShapes { get; set; } = Enum.GetValues(typeof(eDrawShape));
        private eDrawShape _DrawShape = eDrawShape.圆形;
        public eDrawShape DrawShape { get => _DrawShape; set => Set(ref _DrawShape, value, () => SetBurshRegion()); }
        private int _DrawSize = 10;
        public int DrawSize { get => _DrawSize; set => Set(ref _DrawSize, value, () => SetBurshRegion()); }

        [NonSerialized] private bool RoiChanged_Flag = false;
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        [NonSerialized] HRegion finalRegion_Temp = new HRegion(1.0, 1, 1);
        HRegion finalRegion = new HRegion(1.0, 1, 1);
        HRegion brushRegion = new HRegion(10.0, 10, 10);

        private eEditMode _EditMode = eEditMode.正常显示;
        public eEditMode EditMode
        {
            get => _EditMode;
            set
            {
                Set(ref _EditMode, value, () =>
                {
                    switch (_EditMode)
                    {
                        case eEditMode.正常显示:
                            var view = ModuleView as CreateROIView;
                            if (view != null) view.mWindowH.DrawModel = false;
                            break;
                        case eEditMode.绘制涂抹:
                        case eEditMode.擦除涂抹:
                            DrawOrWipe(_EditMode);
                            break;
                    }
                });
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as CreateROIView;
            if (view == null) return;

            ClosedView = true;
            if (view.mWindowH == null)
            {
                view.mWindowH = new VMHWindowControl();
                view.winFormHost.Child = view.mWindowH;
            }

            // 尝试获取图像（可能为空）
            if (DispImage == null || !DispImage.IsInitialized())
            {
                SetDefaultLink();
                if (InputImageLinkText != null)
                    GetDispImage(InputImageLinkText, true);
            }
            else
                GetDispImage(InputImageLinkText, true);

            GetHomMat2D();

            // ========== 关键修改：始终注册鼠标事件并显示ROI（即使无图像） ==========
            view.mWindowH.hControl.MouseUp += HControl_MouseUp;
            IsLoaded_Flag = true;
            // 关键修改：仅当 DispImage 有效时才调用 ShowHRoi()
            if (DispImage != null && DispImage.IsInitialized())
                ShowHRoi();

            IsLoaded_Flag = false;

            DrawSize = 5;
            // 注册参数变化回调
            Rect2Len1.TextChanged = () => RoiChanged();
            Rect2Len2.TextChanged = () => RoiChanged();
            Rect2MidR.TextChanged = () => RoiChanged();
            Rect2MidC.TextChanged = () => RoiChanged();
            Rect2Deg.TextChanged = () => RoiChanged();
            CircleX.TextChanged = () => RoiChanged();
            CircleY.TextChanged = () => RoiChanged();
            CircleRadius.TextChanged = () => RoiChanged();

            PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectedROIType))
                {
                    RoiList.Clear();
                    RoiChanged();
                }
            };
        }

        [NonSerialized] private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                    _ExecuteCommand = new CommandBase(obj => ExeModule());
                return _ExecuteCommand;
            }
        }

        [NonSerialized] private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                    _ConfirmCommand = new CommandBase(obj => (ModuleView as CreateROIView)?.Close());
                return _ConfirmCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1]);
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink: InputImageLinkText = obj.LinkName; break;
                case eLinkCommand.Rect2Len1: Rect2Len1.Text = obj.LinkName; break;
                case eLinkCommand.Rect2Len2: Rect2Len2.Text = obj.LinkName; break;
                case eLinkCommand.Rect2MidR: Rect2MidR.Text = obj.LinkName; break;
                case eLinkCommand.Rect2MidC: Rect2MidC.Text = obj.LinkName; break;
                case eLinkCommand.Rect2Deg: Rect2Deg.Text = obj.LinkName; break;
                case eLinkCommand.CircleX: CircleX.Text = obj.LinkName; break;
                case eLinkCommand.CircleY: CircleY.Text = obj.LinkName; break;
                case eLinkCommand.CircleRadius: CircleRadius.Text = obj.LinkName; break;
            }
        }

        [NonSerialized] private CommandBase _LinkCommand;
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
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.Rect2Len1:
                            case eLinkCommand.Rect2Len2:
                            case eLinkCommand.Rect2MidR:
                            case eLinkCommand.Rect2MidC:
                            case eLinkCommand.Rect2Deg:
                            case eLinkCommand.CircleX:
                            case eLinkCommand.CircleY:
                            case eLinkCommand.CircleRadius:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},{linkCommand}");
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }

        [NonSerialized] private CommandBase _ClearPaintCommand;
        public CommandBase ClearPaintCommand
        {
            get
            {
                if (_ClearPaintCommand == null)
                    _ClearPaintCommand = new CommandBase(obj =>
                    {
                        finalRegion.Dispose();
                        ShowHRoi();
                    });
                return _ClearPaintCommand;
            }
        }
        #endregion

        #region Method
        private bool IsLoaded_Flag = false;
        private void RoiChanged()
        {
            if (RoiChanged_Flag) return;
            RoiChanged_Flag = true;
            IsLoaded_Flag = true;
            try
            {
                RoiList.Remove(ModuleParam.ModuleName + ROIDefine.Rectangle2);
                RoiList.Remove(ModuleParam.ModuleName + ROIDefine.Circle);
                ShowHRoi();
            }
            finally
            {
                RoiChanged_Flag = false;
                IsLoaded_Flag = false;
            }
        }

        #region ROI区域交互
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (IsDrawing) return;
            var view = ModuleView as CreateROIView;
            if (view == null) return;

            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
            if (string.IsNullOrEmpty(index)) return;

            switch (SelectedROIType)
            {
                case eDrawShape.矩形:
                    ROIRectangle2 rectangle2 = (ROIRectangle2)roi;
                    double displayRow = rectangle2.MidR;
                    double displayCol = rectangle2.MidC;
                    double displayPhi = rectangle2.Phi;
                    double displayLen1 = rectangle2.Length1;
                    double displayLen2 = rectangle2.Length2;

                    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                    {
                        HRegion region = rectangle2.GetRegion();
                        region = region.AffineTransRegion(new HHomMat2D(HomMat2D_Inverse), "nearest_neighbor");
                        region.SmallestRectangle2(out double originalRow, out double originalCol, out double originalPhi, out double originalLen1, out double originalLen2);
                        UpdateRect2Parameters(originalRow, originalCol, originalPhi, originalLen1, originalLen2);
                    }
                    else
                        UpdateRect2Parameters(displayRow, displayCol, displayPhi, displayLen1, displayLen2);
                    break;

                case eDrawShape.圆形:
                    ROICircle circle = (ROICircle)roi;
                    double displayCenterY = circle.CenterY;
                    double displayCenterX = circle.CenterX;
                    double displayRadius = circle.Radius;

                    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                    {
                        HRegion region2 = circle.GetRegion();
                        region2 = region2.AffineTransRegion(new HHomMat2D(HomMat2D_Inverse), "nearest_neighbor");
                        region2.SmallestCircle(out double originalRow, out double originalCol, out double originalRadius);
                        UpdateCircleParameters(originalRow, originalCol, originalRadius);
                    }
                    else
                        UpdateCircleParameters(displayCenterY, displayCenterX, displayRadius);
                    break;
            }
            RoiChanged();
        }

        // 修改：支持断开链接并更新数值
        private void UpdateRect2Parameters(double row, double col, double phi, double len1, double len2)
        {
            Rect2.MidR = Math.Round(row, 3);
            Rect2.MidC = Math.Round(col, 3);
            Rect2.Phi = Math.Round(phi, 3);
            Rect2.Length1 = Math.Round(len1, 3);
            Rect2.Length2 = Math.Round(len2, 3);

            void UpdateOrBreak(ref LinkVarModel fieldRef, LinkVarModel current, double newValue, string fmt = "0.000")
            {
                var cb = current.TextChanged;
                LinkVarModel newParam = new LinkVarModel { Text = newValue.ToString(fmt) };
                newParam.TextChanged = cb;
                fieldRef = newParam;
                RaisePropertyChanged(GetParamName(current));
            }

            string GetParamName(LinkVarModel p)
            {
                if (p == Rect2MidR) return nameof(Rect2MidR);
                if (p == Rect2MidC) return nameof(Rect2MidC);
                if (p == Rect2Deg) return nameof(Rect2Deg);
                if (p == Rect2Len1) return nameof(Rect2Len1);
                if (p == Rect2Len2) return nameof(Rect2Len2);
                return "";
            }

            UpdateOrBreak(ref _Rect2MidR, Rect2MidR, Rect2.MidR);
            UpdateOrBreak(ref _Rect2MidC, Rect2MidC, Rect2.MidC);
            UpdateOrBreak(ref _Rect2Deg, Rect2Deg, Rect2.Phi * 180 / Math.PI);
            UpdateOrBreak(ref _Rect2Len1, Rect2Len1, Rect2.Length1);
            UpdateOrBreak(ref _Rect2Len2, Rect2Len2, Rect2.Length2);
        }

        private void UpdateCircleParameters(double centerY, double centerX, double radius)
        {
            Circle.CenterY = Math.Round(centerY, 3);
            Circle.CenterX = Math.Round(centerX, 3);
            Circle.Radius = Math.Round(radius, 3);

            void UpdateOrBreak(ref LinkVarModel fieldRef, LinkVarModel current, double newValue, string fmt = "0.000")
            {
                var cb = current.TextChanged;
                LinkVarModel newParam = new LinkVarModel { Text = newValue.ToString(fmt) };
                newParam.TextChanged = cb;
                fieldRef = newParam;
                RaisePropertyChanged(GetParamName(current));
            }

            string GetParamName(LinkVarModel p)
            {
                if (p == CircleY) return nameof(CircleY);
                if (p == CircleX) return nameof(CircleX);
                if (p == CircleRadius) return nameof(CircleRadius);
                return "";
            }

            UpdateOrBreak(ref _CircleY, CircleY, Circle.CenterY);
            UpdateOrBreak(ref _CircleX, CircleX, Circle.CenterX);
            UpdateOrBreak(ref _CircleRadius, CircleRadius, Circle.Radius);
        }

        public override void ShowHRoi()
        {
            var view = ModuleView as CreateROIView;
            if (view == null) return;

            // 防呆：无有效图像时提示用户并返回，避免后续Halcon操作导致崩溃
            if (DispImage == null || !DispImage.IsInitialized())
            {
                if (!IsLoaded_Flag)
                {
                    MessageView.Ins.MessageBoxShow("请先链接并选择输入图像后再绘制ROI！", eMsgType.Warn);
                }
                return;
            }

            int viewId = DispImage.DispViewID;
            VMHWindowControl mWindowH = (view == null || view.IsClosed) ? ViewDic.GetView(viewId) : view.mWindowH;
            if (mWindowH == null) return;

            ClearRoiAndText();
            mWindowH.ClearROI();

            try
            {
                switch (SelectedROIType)
                {
                    case eDrawShape.矩形:
                        double rectRow, rectCol, rectPhi, rectLen1, rectLen2;
                        try
                        {
                            rectRow = Convert.ToDouble(GetLinkValue(Rect2MidR));
                            rectCol = Convert.ToDouble(GetLinkValue(Rect2MidC));
                            rectPhi = (double)((HTuple)Convert.ToDouble(GetLinkValue(Rect2Deg))).TupleRad();
                            rectLen1 = Convert.ToDouble(GetLinkValue(Rect2Len1));
                            rectLen2 = Convert.ToDouble(GetLinkValue(Rect2Len2));

                            if (HomMat2D != null && HomMat2D.Length > 0)
                            {
                                using (HRegion tempRegion = new HRegion())
                                {
                                    tempRegion.GenRectangle2(rectRow, rectCol, rectPhi, rectLen1, rectLen2);
                                    tempRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                                    tempRegion.SmallestRectangle2(out rectRow, out rectCol, out rectPhi, out rectLen1, out rectLen2);
                                }
                            }
                            mWindowH.WindowH.genRect2(ModuleParam.ModuleName + ROIDefine.Rectangle2, rectRow, rectCol, -rectPhi, rectLen1, rectLen2, ref RoiList);
                        }
                        catch
                        {
                            mWindowH.WindowH.genRect2(ModuleParam.ModuleName + ROIDefine.Rectangle2, 200, 200, 0, 30, 30, ref RoiList);
                        }
                        break;

                    case eDrawShape.圆形:
                        double circleRow, circleCol, circleRadius;
                        try
                        {
                            circleRow = Convert.ToDouble(GetLinkValue(CircleY));
                            circleCol = Convert.ToDouble(GetLinkValue(CircleX));
                            circleRadius = Convert.ToDouble(GetLinkValue(CircleRadius));

                            if (HomMat2D != null && HomMat2D.Length > 0)
                            {
                                using (HRegion tempRegion = new HRegion())
                                {
                                    tempRegion.GenCircle(circleRow, circleCol, circleRadius);
                                    tempRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                                    tempRegion.SmallestCircle(out circleRow, out circleCol, out circleRadius);
                                }
                            }
                            mWindowH.WindowH.genCircle(ModuleParam.ModuleName + ROIDefine.Circle, circleRow, circleCol, circleRadius, ref RoiList);
                        }
                        catch
                        {
                            mWindowH.WindowH.genCircle(ModuleParam.ModuleName + ROIDefine.Circle, 100, 100, 30, ref RoiList);
                        }
                        break;
                }

                // 涂抹区域显示
                if (finalRegion != null && finalRegion.IsInitialized())
                {
                    if (HomMat2D != null && HomMat2D.Length > 0)
                        finalRegion_Temp = finalRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                    else
                        finalRegion_Temp = new HRegion(finalRegion);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.屏蔽范围, "green", new HObject(finalRegion_Temp)));
                }

                // 文字等其他ROI
                foreach (HRoi roi in mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName))
                {
                    if (roi.roiType == HRoiType.文字显示)
                    {
                        HText roiText = (HText)roi;
                        ShowTool.SetFont(mWindowH.hControl.HalconWindow, roiText.size, "false", "false");
                        ShowTool.SetMsg(mWindowH.hControl.HalconWindow, roiText.text, "image", roiText.row, roiText.col, roiText.drawColor, "false");
                    }
                    else
                        mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }
        #endregion

        #region 涂抹功能
        private void SetBurshRegion()
        {
            if (DispImage == null || !DispImage.IsInitialized()) return;
            DispImage.GetImageSize(out HTuple imageWidth, out HTuple imageHeight);
            HObject ho_temp_brush = null;
            switch (_DrawShape)
            {
                case eDrawShape.圆形:
                    HOperatorSet.GenCircle(out ho_temp_brush, imageWidth / 2, imageHeight / 2, DrawSize);
                    break;
                case eDrawShape.矩形:
                    HOperatorSet.GenRectangle1(out ho_temp_brush, 0, 0, DrawSize, DrawSize);
                    break;
            }
            if (ho_temp_brush != null)
            {
                brushRegion.Dispose();
                brushRegion = new HRegion(ho_temp_brush);
            }
        }

        private void DrawOrWipe(eEditMode editMode)
        {
            IsDrawing = true;
            var view = ModuleView as CreateROIView;
            if (view == null) return;
            view.mWindowH.DrawModel = true;
            view.mWindowH.Focus();

            if (!brushRegion.IsInitialized())
            {
              
                return;
            }
            HOperatorSet.AreaCenter(brushRegion, out HTuple areaBrush, out HTuple rowBrush, out HTuple columnBrush);

            string color = "green";
            if (editMode == eEditMode.擦除涂抹 && !finalRegion.IsInitialized())
            {
                
                return;
            }

            HOperatorSet.SetColor(view.mWindowH.hv_window, color);
            HObject ho_Image = new HObject(DispImage);

            try
            {
                HTuple hv_Button = 0;
                while (hv_Button != 4)
                {
                    Application.DoEvents();
                    HTuple hv_Row = -1, hv_Column = -1;
                    try { HOperatorSet.GetMposition(view.mWindowH.hv_window, out hv_Row, out hv_Column, out hv_Button); }
                    catch { hv_Button = 0; }

                    HOperatorSet.SetSystem("flush_graphic", "false");
                    HOperatorSet.DispObj(ho_Image, view.mWindowH.hv_window);
                    ShowHRoi();

                    if (finalRegion != null && finalRegion.IsInitialized())
                    {
                        if (HomMat2D != null && HomMat2D.Length > 0)
                            finalRegion_Temp = finalRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                        else
                            finalRegion_Temp = new HRegion(finalRegion);
                        view.mWindowH.DispObj(finalRegion_Temp, color);
                    }

                    if (hv_Row >= 0 && hv_Column >= 0)
                    {
                        HOperatorSet.VectorAngleToRigid(rowBrush, columnBrush, 0, hv_Row, hv_Column, 0, out HTuple homMat2D);
                        HOperatorSet.AffineTransRegion(brushRegion, out HObject brush_region_affine, homMat2D, "nearest_neighbor");
                        HOperatorSet.DispObj(brush_region_affine, view.mWindowH.hv_window);
                        HOperatorSet.SetSystem("flush_graphic", "true");
                        ShowTool.SetFont(view.mWindowH.hv_window, 20, "true", "false");
                        ShowTool.SetMsg(view.mWindowH.hv_window, "按下鼠标左键涂抹,右键结束!", "window", 20, 20, "green", "false");

                        if (hv_Button == 1)
                        {
                            if (editMode == eEditMode.绘制涂抹)
                            {
                                if (finalRegion_Temp.IsInitialized())
                                {
                                    HOperatorSet.Union2(finalRegion_Temp, brush_region_affine, out HObject union);
                                    finalRegion_Temp.Dispose();
                                    finalRegion_Temp = new HRegion(union);
                                }
                                else
                                {
                                    finalRegion_Temp = new HRegion(brush_region_affine);
                                }
                            }
                            else if (editMode == eEditMode.擦除涂抹)
                            {
                                HOperatorSet.Difference(finalRegion_Temp, brush_region_affine, out HObject diff);
                                finalRegion_Temp.Dispose();
                                finalRegion_Temp = new HRegion(diff);
                            }

                            if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                                finalRegion = finalRegion_Temp.AffineTransRegion(new HHomMat2D(HomMat2D_Inverse), "nearest_neighbor");
                            else
                                finalRegion = new HRegion(finalRegion_Temp);
                        }
                    }
                }
            }
            finally
            {
                EditMode = eEditMode.正常显示;
                view.mWindowH.ClearROI();
                if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0 && finalRegion_Temp?.IsInitialized() == true)
                    finalRegion = finalRegion_Temp.AffineTransRegion(new HHomMat2D(HomMat2D_Inverse), "nearest_neighbor");
                else if (finalRegion_Temp?.IsInitialized() == true)
                    finalRegion = new HRegion(finalRegion_Temp);

                ShowHRoi();
                if (finalRegion_Temp?.IsInitialized() == true)
                    view.mWindowH.DispObj(finalRegion_Temp, "blue");
                view.mWindowH.DrawModel = false;
                IsDrawing = false;
            }
        }
        #endregion
        #endregion
    }
}