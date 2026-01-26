using EventMgrLib;
using HalconDotNet;
using Plugin.MeasureCircle.Views;
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

namespace Plugin.MeasureCircle.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        InitCenterX,
        InitCenterY,
        InitRadius
    }

    [Category("检测识别")]
    [DisplayName("圆形测量")]
    [ModuleImageName("MeasureCircle")]
    [Serializable]
    public class MeasureCircleViewModel : ModuleBase
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
            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                if (!IsOpenWindows)
                {
                    GetDispImage(InputImageLinkText);
                }  
                if (DispImage != null && DispImage.IsInitialized())
                {
                    GetHomMat2D();
                    if (DisenableAffine2d && HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                    {
                        DisenableAffine2d = false;
                        Aff.Affine2d(HomMat2D_Inverse, TempCircle, InitCircle);
                        if (InitCircleChanged_Flag)
                        {
                            InitCenterX.Text = InitCircle.CenterX.ToString();
                            InitCenterY.Text = InitCircle.CenterY.ToString();
                            InitRadius.Text = InitCircle.Radius.ToString();
                        }
                    }
                    if (HomMat2D!=null && HomMat2D.Length>0)
                    {
                        InitCircle.CenterX = Convert.ToDouble(GetLinkValue(InitCenterX));
                        InitCircle.CenterY = Convert.ToDouble(GetLinkValue(InitCenterY));
                        InitCircle.Radius = Convert.ToDouble(GetLinkValue(InitRadius));
                        Aff.Affine2d(HomMat2D, InitCircle, TranCircle);
                    }
                    else
                    {
                        if(!InitCenterX.Text.StartsWith("&"))
                            InitCircle.CenterX = TranCircle.CenterX = TempCircle.CenterX ;
                        else
                            InitCircle.CenterX = TranCircle.CenterX = TempCircle.CenterX = Convert.ToDouble(GetLinkValue(InitCenterX));

                        if (!InitCenterY.Text.StartsWith("&"))
                            InitCircle.CenterY = TranCircle.CenterY = TempCircle.CenterY;
                        else
                            InitCircle.CenterY = TranCircle.CenterY = TempCircle.CenterY = Convert.ToDouble(GetLinkValue(InitCenterY));

                        if (!InitRadius.Text.StartsWith("&"))
                            InitCircle.Radius = TranCircle.Radius = TempCircle.Radius;
                        else
                            InitCircle.Radius = TranCircle.Radius = TempCircle.Radius = Convert.ToDouble(GetLinkValue(InitRadius));
                    }

                    if (TranCircle.Radius <= 0)
                    {
                        OutCircle.CenterX = 0;
                        OutCircle.CenterY = 0;
                        OutCircle.Radius = 0;
                        ShowHRoi();
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                    HImage temp = new HImage();
                    if (DispImage.CountChannels() != 1 )
                    {
                        temp = DispImage.Rgb1ToGray();
                    }
                    else
                    {
                        temp = DispImage;
                    }
                    #region 素质3连
                    DispImage.GetImageSize(out HTuple wid, out HTuple hei);
                    HImage suzhi = new HImage();
                    suzhi.GenImageConst("byte", wid, hei);
                    #endregion
                    HRegion region = new HRegion();
                    region.GenCircle(TranCircle.CenterY, TranCircle.CenterX, TranCircle.Radius);
                    Find_Circle(temp, region, out HObject Circle, out HObject Cross, out HObject ScopeRegion, MeasInfo.Threshold, MeasInfo.Length1, MeasInfo.Length2, REnum.EnumToStr(MeasInfo.MeasSelect), MeasInfo.MeasNum, REnum.EnumToStr(MeasInfo.MeasMode), (int)MeasInfo.MeasMode2, MeasInfo.ExclusionPoint, out HTuple Row, out HTuple Column, out HTuple Radius);
                    //Meas.MeasCircle(DispImage, TranCircle, MeasInfo, null, OutCircle, out HTuple RowList, out HTuple ColList, out HXLDCont m_MeasXLD);
                    // 增加没查到圆判断输出NG
                    if (Row == 0.0)
                    {
                        OutCircle.CenterX = 0;
                        OutCircle.CenterY = 0;
                        OutCircle.Radius = 0;
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", new HObject(ScopeRegion)));
                        ShowHRoi();
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                    else
                    {
                        OutCircle.CenterX = Math.Round( (double)Column,2);
                        OutCircle.CenterY = Math.Round((double)Row, 2); 
                        OutCircle.Radius = Math.Round((double)Radius, 2);
                    }
                    if (ShowResultPoint)
                    {
                        //Gen.GenCross(out HObject m_MeasCross, RowList, ColList, MeasInfo.Length2, new HTuple(45).TupleRad());
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测点, "red", new HObject(Cross)));
                    }
                    if (ShowResultCircle)
                    {
                        Gen.GenCircle(out HObject m_ResultXLD, OutCircle.CenterX, OutCircle.CenterY, OutCircle.Radius);
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", new HObject(Circle)));
                    }
                    if (ShowMeasContour)
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", new HObject(ScopeRegion)));
                    }
                    ShowHRoi();
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
        public override void AddOutputParams()
        {
            AddOutputParam("测量圆", "object", OutCircle);
            AddOutputParam("圆心X", "double", OutCircle.CenterX);
            AddOutputParam("圆心Y", "double", OutCircle.CenterY);
            if (OutPutRealCoordFlag)
            {
                AddOutputParam("半径", "double", OutCircle.Radius * Scale);
                AddOutputParam("直径", "double", OutCircle.Radius * Scale * 2);
            }
            else
            {
                AddOutputParam("半径", "double", OutCircle.Radius);
                AddOutputParam("直径", "double", OutCircle.Radius * 2);
            }
           
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop
        private bool DisenableAffine2d = false;
        private bool InitCircleChanged_Flag = false;
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
        private bool _ShowResultCircle = true;
        /// <summary>显示结果直线 </summary>
        public bool ShowResultCircle
        {
            get { return _ShowResultCircle; }
            set { Set(ref _ShowResultCircle, value); }
        }
        private double _Scale=1;
        public double Scale
        {
            get { return _Scale; }
            set {_Scale=value; RaisePropertyChanged(); }
        }
        private bool _OutPutRealCoordFlag=false;
        public bool OutPutRealCoordFlag
        {
            get { return _OutPutRealCoordFlag; }
            set { _OutPutRealCoordFlag = value; RaisePropertyChanged(); }
        }

        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        /// <summary>
        /// 检测形态信息
        /// </summary>
        public MeasInfoModel MeasInfo { get; set; } = new MeasInfoModel();
        private ROICircle _OutCircle = new ROICircle();
        /// <summary>
        /// 输出直线信息
        /// </summary>
        public ROICircle OutCircle
        {
            get { return _OutCircle; }
            set { Set(ref _OutCircle, value); }
        }
        private LinkVarModel _InitCenterX = new LinkVarModel() { Text = "10" };
        /// <summary>
        /// 变换前-圆信息
        /// </summary>
        public LinkVarModel InitCenterX
        {
            get { return _InitCenterX; }
            set { _InitCenterX = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _InitCenterY = new LinkVarModel() { Text = "10" };
        /// <summary>
        /// 变换前-圆信息
        /// </summary>
        public LinkVarModel InitCenterY
        {
            get { return _InitCenterY; }
            set { _InitCenterY = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _InitRadius = new LinkVarModel() { Text = "10" };
        /// <summary>
        /// 变换前-圆信息
        /// </summary>
        public LinkVarModel InitRadius
        {
            get { return _InitRadius; }
            set { _InitRadius = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 变换前-圆信息
        /// </summary>
        public ROICircle InitCircle { get; set; } = new ROICircle();
        /// <summary>
        /// 变换前-圆信息
        /// </summary>
        public ROICircle TempCircle { get; set; } = new ROICircle();
        /// <summary>
        /// 变换后-圆信息
        /// </summary>
        public ROICircle TranCircle { get; set; } = new ROICircle();
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
            var view = ModuleView as MeasureCircleView;
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
                    InitCircleMethod();
                }
                InitCenterX.TextChanged = new Action(() => { InitCircleChanged(); });
                InitCenterY.TextChanged = new Action(() => { InitCircleChanged(); });
                InitRadius.TextChanged = new Action(() => { InitCircleChanged(); });

                if (roiCircle == null)
                {
                    roiCircle = new ROICircle(Convert.ToDouble(GetLinkValue(InitCenterY)),Convert.ToDouble(GetLinkValue(InitCenterX)), Convert.ToDouble(GetLinkValue(InitRadius)));
                }
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
                case eLinkCommand.InitCenterX:
                    InitCenterX.Text = obj.LinkName;
                    TempCircle.CenterX = Convert.ToDouble(GetLinkValue(InitCenterX));
                    break;
                case eLinkCommand.InitCenterY:
                    InitCenterY.Text = obj.LinkName;
                    TempCircle.CenterY = Convert.ToDouble(GetLinkValue(InitCenterY));
                    break;
                case eLinkCommand.InitRadius:
                    InitRadius.Text = obj.LinkName;
                    TempCircle.Radius = Convert.ToDouble(GetLinkValue(InitRadius));
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
                            case eLinkCommand.InitCenterX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitCenterX");
                                break;
                            case eLinkCommand.InitCenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitCenterY");
                                break;
                            case eLinkCommand.InitRadius:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InitRadius");
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
                        InitCircleMethod();
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
                        var view = this.ModuleView as MeasureCircleView;
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
        private void InitCircleChanged()
        {
            if (InitCircleChanged_Flag == true) return;
            InitCircle.CenterX = Convert.ToDouble(GetLinkValue(InitCenterX));
            InitCircle.CenterY = Convert.ToDouble(GetLinkValue(InitCenterY));
            InitCircle.Radius = Convert.ToDouble(GetLinkValue(InitRadius));
            DisenableAffine2d = true;
            if (roiCircle != null)
            {
                if (HomMat2D != null && DisenableAffine2d && HomMat2D.Length > 0)
                {
                    Aff.Affine2d(HomMat2D, InitCircle, TempCircle);
                    if (InitCircleChanged_Flag)
                    {
                        roiCircle.CenterX = TempCircle.CenterX;
                        TempCircle.CenterX = TempCircle.CenterX;
                        TempCircle.Radius = TempCircle.Radius;
                    }
                }
                else
                {
                    roiCircle.CenterX = InitCircle.CenterX;
                    roiCircle.CenterY = InitCircle.CenterY;
                    roiCircle.Radius = InitCircle.Radius;
                    TempCircle.CenterX = InitCircle.CenterX;
                    TempCircle.CenterY = InitCircle.CenterY;
                    TempCircle.Radius = InitCircle.Radius;
                }
                ExeModule();
                InitCircleMethod();
            }
        }
        ROICircle roiCircle;
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as MeasureCircleView;
                if (view == null) return; ;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length > 0)
                {
                    roiCircle = roi as ROICircle;
                    if (roiCircle != null)
                    {
                        TempCircle.CenterX = Math.Round(roiCircle.CenterX, 3);
                        TempCircle.CenterY = Math.Round(roiCircle.CenterY, 3);
                        TempCircle.Radius = Math.Round(roiCircle.Radius, 3);
                        DisenableAffine2d = true;
                        InitCircleChanged_Flag = true;
                        
                        ExeModule();
                        InitCircleMethod();
                        InitCircleChanged_Flag = false;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        public void InitCircleMethod()
        {
            var view = ModuleView as MeasureCircleView;
            if (view == null)
            {
                return;
            }
            if (TranCircle.FlagLineStyle != null)
            {
                view.mWindowH.WindowH.genCircle(ModuleParam.ModuleName, TranCircle.CenterX, TranCircle.CenterY, TranCircle.Radius, ref RoiList);
            }
            else if (DispImage != null && !RoiList.ContainsKey(ModuleParam.ModuleName))
            {
                view.mWindowH.WindowH.genCircle(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 2, view.mWindowH.hv_imageHeight / 2, 30, ref RoiList);
                TranCircle.CenterX = view.mWindowH.hv_imageHeight / 2;
                TranCircle.CenterY = view.mWindowH.hv_imageHeight / 2;
                TranCircle.Radius = 30;

                InitCenterX.Text = TranCircle.CenterX.ToString();
                InitCenterY.Text = TranCircle.CenterY.ToString();
                InitRadius.Text = TranCircle.Radius.ToString();
            }
            else if (DispImage != null && RoiList.ContainsKey(ModuleParam.ModuleName))
            {
                if ((HomMat2D_Inverse!=null)&&(HomMat2D_Inverse.Length > 0)) 
                {
                    view.mWindowH.WindowH.genCircle(ModuleParam.ModuleName, TranCircle.CenterY, TranCircle.CenterX, TranCircle.Radius, ref RoiList);
                    Aff.Affine2d(HomMat2D_Inverse, TranCircle, InitCircle);
                    InitCircle.CenterX = Math.Round(InitCircle.CenterX, 3);
                    InitCircle.CenterY = Math.Round(InitCircle.CenterY, 3);
                    InitCircle.Radius = Math.Round(InitCircle.Radius, 3);
                    if (InitCircleChanged_Flag)
                    {
                        InitCenterX.Text = InitCircle.CenterX.ToString();
                        InitCenterY.Text = InitCircle.CenterY.ToString();
                        InitRadius.Text = InitCircle.Radius.ToString();
                    }
                }
                else
                {
                    view.mWindowH.WindowH.genCircle(ModuleParam.ModuleName, InitCircle.CenterY, InitCircle.CenterX, InitCircle.Radius, ref RoiList);
                    if (InitCircleChanged_Flag)
                    {
                        InitCenterX.Text = InitCircle.CenterX.ToString();
                        InitCenterY.Text = InitCircle.CenterY.ToString();
                        InitRadius.Text = InitCircle.Radius.ToString();
                    }
                }
            }
        }

        #endregion

        public void Find_Circle(HObject ho_Image, HObject ho_CircleRegion, out HObject ho_Circle,
out HObject ho_Cross, out HObject ho_ScopeRegion, HTuple hv_Gray, HTuple hv_MeasureWidth,
HTuple hv_MeasureHeight, HTuple hv_Select, HTuple hv_NumMeasure, HTuple hv_Transition,
HTuple hv_Direction, HTuple hv_Distance, out HTuple hv_Row, out HTuple hv_Column,
out HTuple hv_Radius)
        {




            // Local iconic variables 

            HObject ho_ObjectSelected = null, ho_Contour = null;

            // Local control variables 

            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_MetrologyHandle = new HTuple(), hv_Cloumn = new HTuple();
            HTuple hv_Index = new HTuple(), hv_Row1 = new HTuple();
            HTuple hv_Column1 = new HTuple(), hv_Number = new HTuple();
            HTuple hv_hRow = new HTuple(), hv_hCol = new HTuple();
            HTuple hv_hAngel = new HTuple(), hv_fall_Angel = new HTuple();
            HTuple hv_Index1 = new HTuple(), hv_Row2 = new HTuple();
            HTuple hv_Column2 = new HTuple(), hv_Phi = new HTuple();
            HTuple hv_Length1 = new HTuple(), hv_Length2 = new HTuple();
            HTuple hv_Angle = new HTuple(), hv_MeasureHandle = new HTuple();
            HTuple hv_RowEdge = new HTuple(), hv_ColumnEdge = new HTuple();
            HTuple hv_Amplitude = new HTuple(), hv_Distance1 = new HTuple();
            HTuple hv_StartPhi = new HTuple(), hv_EndPhi = new HTuple();
            HTuple hv_PointOrder = new HTuple(), hv_RealRow = new HTuple();
            HTuple hv_RealColumn = new HTuple(), hv_i = new HTuple();
            HTuple hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Circle);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            HOperatorSet.GenEmptyObj(out ho_ScopeRegion);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelected);
            HOperatorSet.GenEmptyObj(out ho_Contour);
            hv_Row = new HTuple();
            hv_Cloumn = new HTuple();
            hv_Radius = new HTuple();
            try
            {
                try
                {
                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    HOperatorSet.SetMetrologyModelImageSize(hv_MetrologyHandle, hv_Width, hv_Height);

                    hv_Row.Dispose(); hv_Cloumn.Dispose(); hv_Radius.Dispose();
                    HOperatorSet.SmallestCircle(ho_CircleRegion, out hv_Row, out hv_Column, out hv_Radius);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Index.Dispose();
                        HOperatorSet.AddMetrologyObjectCircleMeasure(hv_MetrologyHandle, hv_Row,
                            hv_Column, hv_Radius, hv_MeasureWidth, hv_MeasureHeight, 1, hv_Gray,
                            (new HTuple("start_phi")).TupleConcat("end_phi"), (new HTuple((new HTuple(0)).TupleRad()
                            )).TupleConcat((new HTuple(360)).TupleRad()), out hv_Index);
                    }


                    //设置测量对象的参数
                    //'negative'从白到黑, 'positive从黑到白'
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_transition",
                        hv_Transition);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "min_score",
                        .1);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "num_measures",
                        hv_NumMeasure);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_select",
                        "first");


                    ho_ScopeRegion.Dispose(); hv_Row1.Dispose(); hv_Column1.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_ScopeRegion, hv_MetrologyHandle,
                        "all", hv_Transition, out hv_Row1, out hv_Column1);

                    hv_Number.Dispose();
                    HOperatorSet.CountObj(ho_ScopeRegion, out hv_Number);


                    hv_hRow.Dispose();
                    hv_hRow = new HTuple();
                    hv_hCol.Dispose();
                    hv_hCol = new HTuple();
                    hv_hAngel.Dispose();
                    hv_hAngel = new HTuple();
                    hv_fall_Angel.Dispose();
                    hv_fall_Angel = new HTuple();
                    HTuple end_val26 = hv_Number;
                    HTuple step_val26 = 1;
                    //获取全图区域
                    HOperatorSet.GetDomain(ho_Image, out HObject ImageRegion);
                    for (hv_Index1 = 1; hv_Index1.Continue(end_val26, step_val26); hv_Index1 = hv_Index1.TupleAdd(step_val26))
                    {
                        ho_ObjectSelected.Dispose();
                        HOperatorSet.SelectObj(ho_ScopeRegion, out ho_ObjectSelected, hv_Index1);
                        hv_Row2.Dispose(); hv_Column2.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                        HOperatorSet.SmallestRectangle2Xld(ho_ObjectSelected, out hv_Row2, out hv_Column2,
                            out hv_Phi, out hv_Length1, out hv_Length2);
                        HOperatorSet.GenRegionContourXld(ho_ObjectSelected, out HObject ho_ObjectSelected1, "filled");
                        HOperatorSet.Intersection(ImageRegion, ho_ObjectSelected1, out ho_ObjectSelected1);
                        HOperatorSet.AreaCenter(ho_ObjectSelected1, out HTuple number1, out HTuple number2, out HTuple number3);
                        if (number1 < 10) continue;

                        //*给矩形赋予角度
                        hv_Angle.Dispose();
                        HOperatorSet.AngleLx(hv_Row, hv_Column, hv_Row2, hv_Column2, out hv_Angle);
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_MeasureHandle.Dispose();
                            HOperatorSet.GenMeasureRectangle2(hv_Row2, hv_Column2, hv_Angle + (((new HTuple(180)).TupleRad()
                                ) * hv_Direction), hv_MeasureWidth, hv_MeasureHeight, hv_Width, hv_Height,
                                "bilinear", out hv_MeasureHandle);
                        }

                        hv_RowEdge.Dispose(); hv_ColumnEdge.Dispose(); hv_Amplitude.Dispose(); hv_Distance1.Dispose();
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 1, hv_Gray, hv_Transition,
                            hv_Select, out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude, out hv_Distance1);


                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_hRow = hv_hRow.TupleConcat(
                                    hv_RowEdge);
                                hv_hRow.Dispose();
                                hv_hRow = ExpTmpLocalVar_hRow;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_hCol = hv_hCol.TupleConcat(
                                    hv_ColumnEdge);
                                hv_hCol.Dispose();
                                hv_hCol = ExpTmpLocalVar_hCol;
                            }
                        }


                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross, hv_hRow, hv_hCol, 6, (new HTuple(45)).TupleRad()
                            );
                    }

                    if ((int)((new HTuple((new HTuple(hv_hRow.TupleLength())).TupleGreater(2))).TupleAnd(
                        new HTuple((new HTuple(hv_hRow.TupleLength())).TupleEqual(new HTuple(hv_hCol.TupleLength()
                        ))))) != 0)
                    {
                        ho_Contour.Dispose();
                        HOperatorSet.GenContourPolygonXld(out ho_Contour, hv_hRow, hv_hCol);
                        hv_Row.Dispose(); hv_Column.Dispose(); hv_Radius.Dispose(); hv_StartPhi.Dispose(); hv_EndPhi.Dispose(); hv_PointOrder.Dispose();
                        HOperatorSet.FitCircleContourXld(ho_Contour, "atukey", -1, 0, 0, 3, 2,
                            out hv_Row, out hv_Column, out hv_Radius, out hv_StartPhi, out hv_EndPhi,
                            out hv_PointOrder);
                        //gen_circle_contour_xld (Circle, Row, Column, Radius, rad(360), rad(0), 'negative', 1)
                        //**距离最大筛选算法
                        hv_RealRow.Dispose();
                        hv_RealRow = new HTuple();
                        hv_RealColumn.Dispose();
                        hv_RealColumn = new HTuple();
                        for (hv_i = 0; (int)hv_i <= (int)((new HTuple(hv_hRow.TupleLength())) - 1); hv_i = (int)hv_i + 1)
                        {
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_Distance1.Dispose();
                                HOperatorSet.DistancePp(hv_hRow.TupleSelect(hv_i), hv_hCol.TupleSelect(
                                    hv_i), hv_Row, hv_Column, out hv_Distance1);
                            }
                            if ((int)(new HTuple(((((hv_Distance1 - hv_Radius)).TupleAbs())).TupleLess(
                                hv_Distance))) != 0)
                            {
                                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                                {
                                    {
                                        HTuple
                                          ExpTmpLocalVar_RealRow = hv_RealRow.TupleConcat(
                                            hv_hRow.TupleSelect(hv_i));
                                        hv_RealRow.Dispose();
                                        hv_RealRow = ExpTmpLocalVar_RealRow;
                                    }
                                }
                                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                                {
                                    {
                                        HTuple
                                          ExpTmpLocalVar_RealColumn = hv_RealColumn.TupleConcat(
                                            hv_hCol.TupleSelect(hv_i));
                                        hv_RealColumn.Dispose();
                                        hv_RealColumn = ExpTmpLocalVar_RealColumn;
                                    }
                                }
                            }

                        }
                        //**筛选后拟合轮廓
                        if ((int)((new HTuple((new HTuple(hv_RealRow.TupleLength())).TupleGreater(
                            2))).TupleAnd(new HTuple((new HTuple(hv_RealRow.TupleLength())).TupleEqual(
                            new HTuple(hv_RealColumn.TupleLength()))))) != 0)
                        {
                            ho_Contour.Dispose();
                            HOperatorSet.GenContourPolygonXld(out ho_Contour, hv_RealRow, hv_RealColumn);
                            hv_Row.Dispose(); hv_Column.Dispose(); hv_Radius.Dispose(); hv_StartPhi.Dispose(); hv_EndPhi.Dispose(); hv_PointOrder.Dispose();
                            HOperatorSet.FitCircleContourXld(ho_Contour, "atukey", -1, 0, 0, 3, 2,
                                out hv_Row, out hv_Column, out hv_Radius, out hv_StartPhi, out hv_EndPhi,
                                out hv_PointOrder);
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                ho_Circle.Dispose();
                                HOperatorSet.GenCircleContourXld(out ho_Circle, hv_Row, hv_Column, hv_Radius,
                                    (new HTuple(360)).TupleRad(), (new HTuple(0)).TupleRad(), "negative",
                                    1);
                            }
                        }
                        else
                        {
                            hv_Row.Dispose();
                            hv_Row = 0;
                            hv_Column.Dispose();
                            hv_Column = 0;
                            hv_Radius.Dispose();
                            hv_Radius = 0;
                            ho_Circle.Dispose();

                        }


                    }
                    else
                    {
                        hv_Row.Dispose();
                        hv_Row = 0;
                        hv_Column.Dispose();
                        hv_Column = 0;
                        hv_Radius.Dispose();
                        hv_Radius = 0;
                        ho_Circle.Dispose();

                    }
                    HOperatorSet.ClearMetrologyObject(hv_MetrologyHandle, "all");



                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);

                    throw new HalconException(hv_Exception);


                }


                ho_ObjectSelected.Dispose();
                ho_Contour.Dispose();

                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_Column.Dispose();
                hv_Index.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_Number.Dispose();
                hv_hRow.Dispose();
                hv_hCol.Dispose();
                hv_hAngel.Dispose();
                hv_fall_Angel.Dispose();
                hv_Index1.Dispose();
                hv_Row2.Dispose();
                hv_Column2.Dispose();
                hv_Phi.Dispose();
                hv_Length1.Dispose();
                hv_Length2.Dispose();
                hv_Angle.Dispose();
                hv_MeasureHandle.Dispose();
                hv_RowEdge.Dispose();
                hv_ColumnEdge.Dispose();
                hv_Amplitude.Dispose();
                hv_Distance1.Dispose();
                hv_StartPhi.Dispose();
                hv_EndPhi.Dispose();
                hv_PointOrder.Dispose();
                hv_RealRow.Dispose();
                hv_RealColumn.Dispose();
                hv_i.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_ObjectSelected.Dispose();
                ho_Contour.Dispose();

                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_Cloumn.Dispose();
                hv_Index.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_Number.Dispose();
                hv_hRow.Dispose();
                hv_hCol.Dispose();
                hv_hAngel.Dispose();
                hv_fall_Angel.Dispose();
                hv_Index1.Dispose();
                hv_Row2.Dispose();
                hv_Column2.Dispose();
                hv_Phi.Dispose();
                hv_Length1.Dispose();
                hv_Length2.Dispose();
                hv_Angle.Dispose();
                hv_MeasureHandle.Dispose();
                hv_RowEdge.Dispose();
                hv_ColumnEdge.Dispose();
                hv_Amplitude.Dispose();
                hv_Distance1.Dispose();
                hv_StartPhi.Dispose();
                hv_EndPhi.Dispose();
                hv_PointOrder.Dispose();
                hv_RealRow.Dispose();
                hv_RealColumn.Dispose();
                hv_i.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }
    }



}
