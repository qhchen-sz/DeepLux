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
                //ROICircle rOICircle2 = new ROICircle();
                //ROICircle rOICircle3 = new ROICircle();
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
                        //Aff.Affine2d(HomMat2D_Inverse, rOICircle, rOICircle2);
                        //Aff.Affine2d(HomMat2D, rOICircle2, rOICircle3);
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
                    string Select = GetEnumDescription(MeasInfo.MeasSelect);
                    string Modes = GetEnumDescription( MeasInfo.MeasMode);
                    FindLineTools.Find_HoLine(DispImage, out HObject Line, out HObject region, TranLine.MidR, TranLine.MidC, -TranLine.Phi, TranLine.Length1, TranLine.Length2, (int)MeasInfo.Threshold, (int)MeasInfo.MeasDis, Modes,
                       Select, 0.1, out HTuple RowBegin, out HTuple ColBegin, out HTuple RowEnd, out HTuple ColEnd);
                    //Meas.MeasLine(DispImage, TranLine, MeasInfo, OutLine, out HTuple RowList, out HTuple ColList, out HXLDCont m_MeasXLD, null);
                    //if (ShowResultPoint && RowList.ToDArr().Length > 0) //显示结果点]
                    //{
                    //    Gen.GenCross(out HObject m_MeasCross, RowList, ColList, MeasInfo.Length2, new HTuple(45).TupleRad());
                    //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测点, "red", new HObject(m_MeasCross)));
                    //}
                    if (ShowResultLine && Line != null && Line.IsInitialized()) //显示结果线
                    {
                       
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", Line));
                    }
                    if (ShowMeasContour) //显示检测范围
                    {
                        if (region != null && region.IsInitialized())
                        {
                            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", region));
                        }
                    }
                    if (ColBegin.Length != 0)
                    {
                        OutLine.StartX = Math.Round((double)ColBegin, 2);
                        OutLine.StartY = Math.Round((double)RowBegin, 2);
                        OutLine.EndX = Math.Round((double)ColEnd, 2);
                        OutLine.EndY = Math.Round((double)RowEnd, 2);
                    }
                    else
                    {
                        OutLine.StartX = 0;
                        OutLine.StartY = 0;
                        OutLine.EndX = 0;
                        OutLine.EndY = 0;
                    }


                    ShowHRoi();
                    //if (RowList.ToDArr().Length > 0)
                    //{
                    //    OutLine.Status = true;
                    //    ChangeModuleRunStatus(eRunStatus.OK);
                    //    return true;
                    //}
                    //else
                    //{
                    //    OutLine.Status = false;
                    //    ChangeModuleRunStatus(eRunStatus.NG);
                    //    return false;
                    //}
                    ChangeModuleRunStatus(eRunStatus.OK);
                    return true;
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
            //OutLine.Status == eRunStatus.OK ? true : false; 
            AddOutputParam("测量直线", "object", OutLine);
            AddOutputParam("中心X", "double", (OutLine.StartX + OutLine.EndX)/2);
            AddOutputParam("中心Y", "double", (OutLine.StartY + OutLine.EndY) / 2);
            AddOutputParam("角度", "double", OutLine.Phi);
            //AddOutputParam("起点X", "double", OutLine.StartX);
            //AddOutputParam("起点Y", "double", OutLine.StartY);
            //AddOutputParam("终点X", "double", OutLine.EndX);
            //AddOutputParam("终点Y", "double", OutLine.EndY);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop
        private bool DisenableAffine2d = false;
        private bool InitLineChanged_Flag = false;
        private bool _ShowResultPoint = true;
        /// <summary>显示结果点</summary>
        public bool ShowResultPoint
        {
            get { return _ShowResultPoint; }
            set { Set(ref _ShowResultPoint, value); }
        }
        private bool _ShowMeasContour = true;
        /// <summary>显示测量轮廓 </summary>
        public bool ShowMeasContour
        {
            get { return _ShowMeasContour; }
            set { Set(ref _ShowMeasContour, value); }
        }
        private bool _ShowResultLine = true;
        /// <summary>显示结果直线 </summary>
        public bool ShowResultLine
        {
            get { return _ShowResultLine; }
            set { Set(ref _ShowResultLine, value); }
        }
        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        /// <summary>
        /// 检测形态信息
        /// </summary>
        public MeasInfoModel MeasInfo { get; set; } = new MeasInfoModel();
        private ROILine _OutLine = new ROILine();
        //private ROILine _OutLine = new ROILine();
        /// <summary>
        /// 输出直线信息
        /// </summary>
        /// 
        public ROILine OutLine
        {
            get { return _OutLine; }
            set { Set(ref _OutLine, value); }
        }
        //public ROILine OutLine
        //{
        //    get { return _OutLine; }
        //    set { Set(ref _OutLine, value); }
        //}
        /// <summary>
        /// 变换前-直线信息
        /// </summary>
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
        /// <summary>
        /// 直线信息
        /// </summary>
        public ROIRectangle2 InitLine { get; set; } = new ROIRectangle2();
        public ROIRectangle2 TempLine { get; set; } = new ROIRectangle2();
        /// <summary>
        /// 变换后-直线信息
        /// </summary>
        public ROIRectangle2 TranLine { get; set; } = new ROIRectangle2();
        private eShieldRegion _ShieldRegion = eShieldRegion.手绘区域;
        /// <summary>
        /// 屏蔽区域
        /// </summary>
        public eShieldRegion ShieldRegion
        {
            get { return _ShieldRegion; }
            set { _ShieldRegion = value; RaisePropertyChanged(); }
        }
        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
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
                    //以GUID+类名作为筛选器
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
                        //ROIRectangle2 temp = new ROIRectangle2(TempLine.MidR , TempLine.MidC,TempLine.Phi, TempLine.Length1, TempLine.Length2);
                        //Aff.Affine2d(HomMat2D_Inverse,  TempLine,temp);
                        //Aff.Affine2d(HomMat2D, TempLine, temp);
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
            if (view == null)
            {
                return;
            }
            if (TranLine.FlagLineStyle != null)
            {
                view.mWindowH.WindowH.genRect2(ModuleParam.ModuleName, TranLine.MidR, TranLine.MidC, TranLine.Phi, TranLine.Length1, TranLine.Length2,ref RoiList);
            }
            else if (DispImage != null && !RoiList.ContainsKey(ModuleParam.ModuleName))
            {
                //view.mWindowH.WindowH.genLine(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageWidth / 2, ref RoiList);
                //TranLine.MidC = view.mWindowH.hv_imageHeight / 4;
                //TranLine.MidR = view.mWindowH.hv_imageHeight / 4;
                //TranLine.Length1 = view.mWindowH.hv_imageHeight / 4;
                //TranLine.Length2 = view.mWindowH.hv_imageWidth / 4;
                view.mWindowH.WindowH.genRect2(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageWidth / 4, 0,50,50 ,ref RoiList);
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
                        //InitLineStartX.Text = InitLine.StartX.ToString();
                        //InitLineStartY.Text = InitLine.StartY.ToString();
                        //InitLineEndX.Text = InitLine.EndX.ToString();
                        //InitLineEndY.Text = InitLine.EndY.ToString();
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
    }
}
