using EventMgrLib;
using HalconDotNet;
using Plugin.MeasureLine.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Documents;
using System.Windows.Forms;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Newtonsoft.Json.Linq;

namespace Plugin.MeasureLine.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        InitLineCenterX,
        InitLineCenterY,
        InitLineLength1,
        InitLineLength2,
        InitLineAngel,
    }
    #endregion

    [Category("检测识别")]
    [DisplayName("直线工具")]
    [ModuleImageName("MeasureLine")]
    [Serializable]
    public class MeasureLineViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            if (InputImageLinkText == null)
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
                if (!IsOpenWindows)
                    GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    GetHomMat2D();
                    if (DisenableAffine2d && HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                    {
                        DisenableAffine2d = false;
                        ROICircle rOICircle = new ROICircle()
                        {
                            CenterX = TempLine.MidC,
                            CenterY = TempLine.MidR,
                            Radius = 10
                        };

                        Aff.Affine2d(HomMat2D_Inverse, TempLine, InitLine);
                        if (InitLineChanged_Flag)
                        {
                            InitLineCenterX.Text = InitLine.MidC.ToString();
                            InitLineCenterY.Text = InitLine.MidR.ToString();
                            InitLineLength1.Text = InitLine.Length1.ToString();
                            InitLineLength2.Text = InitLine.Length2.ToString();
                            InitLineAngel.Text = InitLine.Deg.ToString();
                        }
                    }
                    if (HomMat2D != null && HomMat2D.Length > 0)
                    {
                        InitLine.MidC = Convert.ToDouble(GetLinkValue(InitLineCenterX));
                        InitLine.MidR = Convert.ToDouble(GetLinkValue(InitLineCenterY));
                        InitLine.Length1 = Convert.ToDouble(GetLinkValue(InitLineLength1));
                        InitLine.Length2 = Convert.ToDouble(GetLinkValue(InitLineLength2));
                        InitLine.Deg = Convert.ToDouble(GetLinkValue(InitLineAngel));
                        Aff.Affine2d(HomMat2D, InitLine, TranLine);
                    }
                    else
                    {
                        InitLine.MidC = TranLine.MidC = TempLine.MidC;
                        InitLine.MidR = TranLine.MidR = TempLine.MidR;
                        InitLine.Length1 = TranLine.Length1 = TempLine.Length1;
                        InitLine.Length2 = TranLine.Length2 = TempLine.Length2;
                        InitLine.Deg = TranLine.Deg = TempLine.Deg;
                    }

                    // 执行直线检测（原方法，但扩展了点集输出）
                    string Select = GetEnumDescription(MeasInfo.MeasSelect);
                    string Modes = GetEnumDescription(MeasInfo.MeasMode);
                    HTuple measureRows = null, measureCols = null;
                    bool success = FindLineTools.Find_HoLine(DispImage, out HObject lineContour, out HObject region,
                        TranLine.MidR, TranLine.MidC, -TranLine.Phi, TranLine.Length1, TranLine.Length2,
                        (int)MeasInfo.Threshold, (int)MeasInfo.MeasDis, Modes, Select, 0.1,
                        out HTuple RowBegin, out HTuple ColBegin, out HTuple RowEnd, out HTuple ColEnd,
                        out measureRows, out measureCols);

                    // 输出直线结果
                    if (ColBegin.Length != 0)
                    {
                        OutLine.StartX = Math.Round((double)ColBegin, 2);
                        OutLine.StartY = Math.Round((double)RowBegin, 2);
                        OutLine.EndX = Math.Round((double)ColEnd, 2);
                        OutLine.EndY = Math.Round((double)RowEnd, 2);

                        double deltaX = OutLine.EndX - OutLine.StartX;
                        double deltaY = OutLine.EndY - OutLine.StartY;
                        double angleRad = Math.Atan2(deltaY, deltaX);
                        double angleDeg = Math.Round(angleRad * 180 / Math.PI, 2);
                        if (angleDeg > 90) angleDeg -= 180;
                        if (angleDeg < -90) angleDeg += 180;
                        OutLine.Phi = angleDeg;

                        // 直线度计算（如果启用且有测量点）
                        if (OutputStraightness && measureRows != null && measureRows.Length > 0)
                        {
                            double A = OutLine.EndY - OutLine.StartY;
                            double B = OutLine.StartX - OutLine.EndX;
                            double C = OutLine.EndX * OutLine.StartY - OutLine.StartX * OutLine.EndY;
                            double denom = Math.Sqrt(A * A + B * B);
                            if (denom > 1e-6)
                            {
                                double maxDist = 0;
                                for (int i = 0; i < measureRows.Length; i++)
                                {
                                    double r = measureRows[i].D;
                                    double c = measureCols[i].D;
                                    double dist = Math.Abs(A * c + B * r + C) / denom;
                                    if (dist > maxDist) maxDist = dist;
                                }
                                Straightness = Math.Round(maxDist, 3);
                            }
                            else
                            {
                                Straightness = -1;
                            }
                        }
                        else
                        {
                            Straightness = -1;
                        }
                    }
                    else
                    {
                        OutLine.StartX = OutLine.StartY = OutLine.EndX = OutLine.EndY = 0;
                        OutLine.Phi = 0;
                        Straightness = -1;
                    }

                    // 显示结果
                    if (ShowResultLine && lineContour != null && lineContour.IsInitialized())
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", lineContour));
                    }
                    if (ShowMeasContour && region != null && region.IsInitialized())
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", region));
                    }
                    if (ShowResultPoint && measureRows != null && measureRows.Length > 0)
                    {
                        HObject cross;
                        HOperatorSet.GenCrossContourXld(out cross, measureRows, measureCols, MeasInfo.Length2 / 2, new HTuple(45).TupleRad());
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测点, "red", cross));
                    }
                    ShowHRoi();

                    ChangeModuleRunStatus(success && ColBegin.Length != 0 ? eRunStatus.OK : eRunStatus.NG);
                    return success && ColBegin.Length != 0;
                }
                else
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }

        public static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(EnumDescriptionAttribute), false)
                            .FirstOrDefault() as EnumDescriptionAttribute;
            return attribute?.Description ?? value.ToString();
        }

        public override void AddOutputParams()
        {
            AddOutputParam("测量直线", "object", OutLine);
            AddOutputParam("中心X", "double", (OutLine.StartX + OutLine.EndX) / 2);
            AddOutputParam("中心Y", "double", (OutLine.StartY + OutLine.EndY) / 2);
            AddOutputParam("角度", "double", OutLine.Phi);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
            if (OutputStraightness)
            {
                AddOutputParam("直线度", "double", Straightness);
            }
        }

        #region Prop
        private bool DisenableAffine2d = false;
        private bool InitLineChanged_Flag = false;
        private bool _ShowResultPoint = true;
        public bool ShowResultPoint
        {
            get { return _ShowResultPoint; }
            set { Set(ref _ShowResultPoint, value); }
        }
        private bool _ShowMeasContour = true;
        public bool ShowMeasContour
        {
            get { return _ShowMeasContour; }
            set { Set(ref _ShowMeasContour, value); }
        }
        private bool _ShowResultLine = true;
        public bool ShowResultLine
        {
            get { return _ShowResultLine; }
            set { Set(ref _ShowResultLine, value); }
        }

        private bool _OutputStraightness = false;
        /// <summary>是否输出直线度</summary>
        public bool OutputStraightness
        {
            get { return _OutputStraightness; }
            set { Set(ref _OutputStraightness, value); }
        }

        private double _Straightness = -1;
        /// <summary>直线度（最大偏差，像素单位）</summary>
        public double Straightness
        {
            get { return _Straightness; }
            set { Set(ref _Straightness, value); }
        }

        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        public MeasInfoModel MeasInfo { get; set; } = new MeasInfoModel();
        private ROILine _OutLine = new ROILine();
        public ROILine OutLine
        {
            get { return _OutLine; }
            set { Set(ref _OutLine, value); }
        }

        private LinkVarModel _InitLineCenterX = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitLineCenterX
        {
            get { return _InitLineCenterX; }
            set { _InitLineCenterX = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _InitLineCenterY = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitLineCenterY
        {
            get { return _InitLineCenterY; }
            set { _InitLineCenterY = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _InitLineLength1 = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitLineLength1
        {
            get { return _InitLineLength1; }
            set { _InitLineLength1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _InitLineLength2 = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitLineLength2
        {
            get { return _InitLineLength2; }
            set { _InitLineLength2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _InitLineAngel = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitLineAngel
        {
            get { return _InitLineAngel; }
            set { _InitLineAngel = value; RaisePropertyChanged(); }
        }
        public ROIRectangle2 InitLine { get; set; } = new ROIRectangle2();
        public ROIRectangle2 TempLine { get; set; } = new ROIRectangle2();
        public ROIRectangle2 TranLine { get; set; } = new ROIRectangle2();
        private eShieldRegion _ShieldRegion = eShieldRegion.手绘区域;
        public eShieldRegion ShieldRegion
        {
            get { return _ShieldRegion; }
            set { _ShieldRegion = value; RaisePropertyChanged(); }
        }
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    ShowHRoi();
                }
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as MeasureLineView;
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
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.mWindowH.HobjectToHimage(DispImage);
                    view.mWindowH.hControl.MouseUp += HControl_MouseUp;
                    ShowHRoi();
                    InitLineMethod();
                }
                InitLineCenterX.TextChanged = new Action(() => { InitLineChanged(); });
                InitLineCenterY.TextChanged = new Action(() => { InitLineChanged(); });
                InitLineLength1.TextChanged = new Action(() => { InitLineChanged(); });
                InitLineLength2.TextChanged = new Action(() => { InitLineChanged(); });
                InitLineAngel.TextChanged = new Action(() => { InitLineChanged(); });
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.InitLineCenterX:
                    InitLineCenterX.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitLineCenterY:
                    InitLineCenterY.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitLineLength1:
                    InitLineLength1.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitLineLength2:
                    InitLineLength2.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitLineAngel:
                    InitLineAngel.Text = obj.LinkName;
                    break;
                default:
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
                            case eLinkCommand.InitLineCenterX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitLineCenterX");
                                break;
                            case eLinkCommand.InitLineCenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitLineCenterY");
                                break;
                            case eLinkCommand.InitLineLength1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitLineLength1");
                                break;
                            case eLinkCommand.InitLineLength2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitLineLength2");
                                break;
                            case eLinkCommand.InitLineAngel:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitLineAngel");
                                break;
                            default:
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
                    _ExecuteCommand = new CommandBase((obj) =>
                    {
                        ExeModule();
                        InitLineMethod();
                    });
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
                        var view = this.ModuleView as MeasureLineView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        #endregion

        #region Method
        private void InitLineChanged()
        {
            if (InitLineChanged_Flag == true) return;
            InitLine.MidC = Convert.ToDouble(GetLinkValue(InitLineCenterX));
            InitLine.MidR = Convert.ToDouble(GetLinkValue(InitLineCenterY));
            InitLine.Length1 = Convert.ToDouble(GetLinkValue(InitLineLength1));
            InitLine.Length2 = Convert.ToDouble(GetLinkValue(InitLineLength2));
            InitLine.Deg = Convert.ToDouble(GetLinkValue(InitLineAngel));
            DisenableAffine2d = true;
            if (roiLine != null)
            {
                if (DisenableAffine2d && HomMat2D != null && HomMat2D.Length > 0)
                {
                    Aff.Affine2d(HomMat2D, InitLine, TempLine);
                    if (InitLineChanged_Flag)
                    {
                        roiLine.MidC = TempLine.MidC;
                        roiLine.MidR = TempLine.MidR;
                        roiLine.Length1 = TempLine.Length1;
                        roiLine.Length2 = TempLine.Length2;
                        roiLine.Deg = TempLine.Deg;
                    }
                }
                else
                {
                    roiLine.MidC = InitLine.MidC;
                    roiLine.MidR = InitLine.MidR;
                    roiLine.Length1 = InitLine.Length1;
                    roiLine.Length2 = InitLine.Length2;
                    roiLine.Deg = InitLine.Deg;
                    TempLine.MidC = InitLine.MidC;
                    TempLine.MidR = InitLine.MidR;
                    TempLine.Length1 = InitLine.Length1;
                    TempLine.Length2 = InitLine.Length2;
                    TempLine.Deg = InitLine.Deg;
                }
                ExeModule();
                InitLineMethod();
            }
        }

        ROIRectangle2 roiLine;
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as MeasureLineView;
                if (view == null) return;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length > 0)
                {
                    roiLine = roi as ROIRectangle2;
                    if (roiLine != null)
                    {
                        TempLine.MidC = Math.Round(roiLine.MidC, 3);
                        TempLine.MidR = Math.Round(roiLine.MidR, 3);
                        TempLine.Length1 = Math.Round(roiLine.Length1, 3);
                        TempLine.Length2 = Math.Round(roiLine.Length2, 3);
                        TempLine.Deg = Math.Round(roiLine.Deg, 3);
                        DisenableAffine2d = true;
                        InitLineChanged_Flag = true;
                        ExeModule();
                        InitLineMethod();
                        InitLineChanged_Flag = false;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void InitLineMethod()
        {
            var view = ModuleView as MeasureLineView;
            if (view == null) return;

            if (TranLine.FlagLineStyle != null)
            {
                view.mWindowH.WindowH.genRect2(ModuleParam.ModuleName, TranLine.MidR, TranLine.MidC, TranLine.Phi, TranLine.Length1, TranLine.Length2, ref RoiList);
            }
            else if (DispImage != null && !RoiList.ContainsKey(ModuleParam.ModuleName))
            {
                view.mWindowH.WindowH.genRect2(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageWidth / 4, 0, 50, 50, ref RoiList);
                TranLine.MidC = view.mWindowH.hv_imageWidth / 4;
                TranLine.MidR = view.mWindowH.hv_imageHeight / 4;
                TranLine.Length1 = 50;
                TranLine.Length2 = 50;
                TranLine.Deg = 0;
            }
            else if (DispImage != null && RoiList.ContainsKey(ModuleParam.ModuleName))
            {
                if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    view.mWindowH.WindowH.genRect2(ModuleParam.ModuleName, TranLine.MidR, TranLine.MidC, TranLine.Phi, TranLine.Length1, TranLine.Length2, ref RoiList);
                    Aff.Affine2d(HomMat2D_Inverse, TranLine, InitLine);
                    InitLine.MidC = Math.Round(InitLine.MidC, 3);
                    InitLine.MidR = Math.Round(InitLine.MidR, 3);
                    InitLine.Length1 = Math.Round(InitLine.Length1, 3);
                    InitLine.Length2 = Math.Round(InitLine.Length2, 3);
                    InitLine.Deg = Math.Round(InitLine.Deg, 3);
                    if (InitLineChanged_Flag)
                    {
                        InitLineCenterX.Text = InitLine.MidC.ToString();
                        InitLineCenterY.Text = InitLine.MidR.ToString();
                        InitLineLength1.Text = InitLine.Length1.ToString();
                        InitLineLength2.Text = InitLine.Length2.ToString();
                        InitLineAngel.Text = InitLine.Deg.ToString();
                    }
                }
                else
                {
                    view.mWindowH.WindowH.genRect2(ModuleParam.ModuleName, InitLine.MidR, InitLine.MidC, InitLine.Phi, InitLine.Length1, InitLine.Length2, ref RoiList);
                    if (InitLineChanged_Flag)
                    {
                        InitLineCenterX.Text = InitLine.MidC.ToString();
                        InitLineCenterY.Text = InitLine.MidR.ToString();
                        InitLineLength1.Text = InitLine.Length1.ToString();
                        InitLineLength2.Text = InitLine.Length2.ToString();
                        InitLineAngel.Text = InitLine.Deg.ToString();
                    }
                }
            }
        }
        #endregion

        #region Serialize
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["ShowResultPoint"] = ShowResultPoint;
            obj["ShowMeasContour"] = ShowMeasContour;
            obj["ShowResultLine"] = ShowResultLine;
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["InitLineCenterX"] = InitLineCenterX?.Text ?? "";
            obj["InitLineCenterY"] = InitLineCenterY?.Text ?? "";
            obj["InitLineLength1"] = InitLineLength1?.Text ?? "";
            obj["InitLineLength2"] = InitLineLength2?.Text ?? "";
            obj["InitLineAngel"] = InitLineAngel?.Text ?? "";
            obj["ShieldRegion"] = (int)ShieldRegion;
            JObject measObj = new JObject();
            if (MeasInfo != null)
            {
                measObj["MeasDis"] = MeasInfo.MeasDis;
                measObj["Length1"] = MeasInfo.Length1;
                measObj["Length2"] = MeasInfo.Length2;
                measObj["Threshold"] = MeasInfo.Threshold;
                measObj["MeasMode"] = (int)MeasInfo.MeasMode;
                measObj["MeasSelect"] = (int)MeasInfo.MeasSelect;
                measObj["PointsOrder"] = MeasInfo.PointsOrder;
            }
            obj["MeasInfo"] = measObj;
            obj["InitLine"] = SerializeRect2(InitLine);
            obj["TempLine"] = SerializeRect2(TempLine);
            obj["TranLine"] = SerializeRect2(TranLine);
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["ShowResultPoint"] != null) ShowResultPoint = obj["ShowResultPoint"].Value<bool>();
                if (obj["ShowMeasContour"] != null) ShowMeasContour = obj["ShowMeasContour"].Value<bool>();
                if (obj["ShowResultLine"] != null) ShowResultLine = obj["ShowResultLine"].Value<bool>();
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["InitLineCenterX"] != null && InitLineCenterX != null) InitLineCenterX.Text = obj["InitLineCenterX"].ToString();
                if (obj["InitLineCenterY"] != null && InitLineCenterY != null) InitLineCenterY.Text = obj["InitLineCenterY"].ToString();
                if (obj["InitLineLength1"] != null && InitLineLength1 != null) InitLineLength1.Text = obj["InitLineLength1"].ToString();
                if (obj["InitLineLength2"] != null && InitLineLength2 != null) InitLineLength2.Text = obj["InitLineLength2"].ToString();
                if (obj["InitLineAngel"] != null && InitLineAngel != null) InitLineAngel.Text = obj["InitLineAngel"].ToString();
                if (obj["ShieldRegion"] != null) ShieldRegion = (eShieldRegion)obj["ShieldRegion"].Value<int>();
                if (obj["MeasInfo"] != null && MeasInfo != null)
                {
                    JObject measObj = (JObject)obj["MeasInfo"];
                    if (measObj["MeasDis"] != null) MeasInfo.MeasDis = measObj["MeasDis"].Value<double>();
                    if (measObj["Length1"] != null) MeasInfo.Length1 = measObj["Length1"].Value<double>();
                    if (measObj["Length2"] != null) MeasInfo.Length2 = measObj["Length2"].Value<double>();
                    if (measObj["Threshold"] != null) MeasInfo.Threshold = measObj["Threshold"].Value<double>();
                    if (measObj["MeasMode"] != null) MeasInfo.MeasMode = (eMeasMode)measObj["MeasMode"].Value<int>();
                    if (measObj["MeasSelect"] != null) MeasInfo.MeasSelect = (eMeasSelect)measObj["MeasSelect"].Value<int>();
                    if (measObj["PointsOrder"] != null) MeasInfo.PointsOrder = measObj["PointsOrder"].Value<int>();
                }
                if (obj["InitLine"] != null) DeserializeRect2((JObject)obj["InitLine"], InitLine);
                if (obj["TempLine"] != null) DeserializeRect2((JObject)obj["TempLine"], TempLine);
                if (obj["TranLine"] != null) DeserializeRect2((JObject)obj["TranLine"], TranLine);
            }
            catch (Exception ex)

            {

                Logger.AddLog($"MeasureLineViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        private JObject SerializeRect2(ROIRectangle2 rect)
        {
            if (rect == null) return null;
            JObject obj = new JObject();
            obj["MidR"] = rect.MidR;
            obj["MidC"] = rect.MidC;
            obj["Length1"] = rect.Length1;
            obj["Length2"] = rect.Length2;
            obj["Phi"] = rect.Phi;
            obj["Deg"] = rect.Deg;
            return obj;
        }

        private void DeserializeRect2(JObject obj, ROIRectangle2 rect)
        {
            if (obj == null || rect == null) return;
            if (obj["MidR"] != null) rect.MidR = obj["MidR"].Value<double>();
            if (obj["MidC"] != null) rect.MidC = obj["MidC"].Value<double>();
            if (obj["Length1"] != null) rect.Length1 = obj["Length1"].Value<double>();
            if (obj["Length2"] != null) rect.Length2 = obj["Length2"].Value<double>();
            if (obj["Phi"] != null) rect.Phi = obj["Phi"].Value<double>();
            if (obj["Deg"] != null) rect.Deg = obj["Deg"].Value<double>();
        }
        #endregion
    }
}