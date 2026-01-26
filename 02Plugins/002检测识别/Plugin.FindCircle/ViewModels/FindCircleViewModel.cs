using EventMgrLib;
using HalconDotNet;
using Plugin.FindCircle.Views;
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

namespace Plugin.FindCircle.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        InitCenterX,
        InitCenterY,
        InitRadius
    }

    [Category("检测识别")]
    [DisplayName("螺丝钉检测")]
    [ModuleImageName("FindCircle")]
    [Serializable]
    public class FindCircleViewModel : ModuleBase
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
                if (ModuleView == null || ModuleView.IsClosed)
                    GetDispImage(InputImageLinkText);
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
                        InitCircle.CenterX = TranCircle.CenterX = TempCircle.CenterX;
                        InitCircle.CenterY = TranCircle.CenterY = TempCircle.CenterY;
                        InitCircle.Radius = TranCircle.Radius = TempCircle.Radius;
                    }
                    //Meas.MeasCircle(DispImage, TranCircle, MeasInfo, null, OutCircle, out HTuple RowList, out HTuple ColList, out HXLDCont m_MeasXLD);

                    DispImage.GetImageSize(out HTuple wid, out HTuple hei);
                    HImage temp = new HImage();
                    temp.GenImageConst("byte", wid, hei);

                    TranCircle.GetRegion().SmallestCircle(out HTuple Y, out HTuple X, out HTuple Radius);
                    FindCircle(DispImage, TranCircle.GetRegion(), out HObject MeasContour, out HObject ResultCircle, out HObject Cross, 80, 20, 10, out HTuple row, out HTuple col, out HTuple radius);
                    ///增加没查到圆判断输出NG
                    if (row.Length == 0)
                    {   
                        OutCircle.CenterX=0;
                        OutCircle.CenterY=0;    
                        OutCircle.Radius=0;
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", TranCircle.GetRegion()));
                        ShowHRoi();
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                    else
                    {
                        OutCircle.CenterX = col[0];
                        OutCircle.CenterY = row[0];
                        OutCircle.Radius = radius[0];
                    }
                    if (ShowResultPoint)
                    {
                        //Gen.GenCross(out HObject m_MeasCross, RowList, ColList, MeasInfo.Length2, new HTuple(45).TupleRad());
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测点, "red", Cross));
                    }
                    if (ShowResultCircle)
                    {
                        //Gen.GenCircle(out HObject m_ResultXLD, OutCircle.CenterX, OutCircle.CenterY, OutCircle.Radius);
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", ResultCircle));
                    }
                    if (ShowMeasContour)
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", MeasContour));
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
            var view = ModuleView as FindCircleView;
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
                    break;
                case eLinkCommand.InitCenterY:
                    InitCenterY.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitRadius:
                    InitCenterX.Text = obj.LinkName;
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
                        var view = this.ModuleView as FindCircleView;
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
                if (DisenableAffine2d && HomMat2D!=null && HomMat2D.Length > 0)
                {
                    Aff.Affine2d(HomMat2D, InitCircle, TempCircle);
                    if (InitCircleChanged_Flag)
                    {
                        roiCircle.CenterX = TempCircle.CenterX;
                        TempCircle.CenterY = TempCircle.CenterY;
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
                var view = ModuleView as FindCircleView;
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
            var view = ModuleView as FindCircleView;
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
        private static void FindCircle(HObject ho_Image, HObject ho_Region, out HObject ho_MeeasureRegion,
      out HObject ho_Contour, out HObject ho_Cross, HTuple hv_Measure_Threshold, HTuple hv_Measure_Num,
      HTuple hv_Measure_Height, out HTuple hv_Row, out HTuple hv_Column, out HTuple hv_Radius)
        {




            // Local iconic variables 

            HObject ho_ImageReduced = null, ho_RegionFillUp = null;
            HObject ho_Region1 = null, ho_RegionIntersection = null, ho_RegionClosing = null;
            HObject ho_RegionFillUp1 = null, ho_ConnectedRegions = null;

            // Local copy input parameter variables 
            HObject ho_Region_COPY_INP_TMP;
            ho_Region_COPY_INP_TMP = new HObject(ho_Region);



            // Local control variables 

            HTuple hv_MetrologyHandle = new HTuple(), hv_Index1 = new HTuple();
            HTuple hv_Row1 = new HTuple(), hv_Column1 = new HTuple();
            HTuple hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_MeeasureRegion);
            HOperatorSet.GenEmptyObj(out ho_Contour);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_RegionIntersection);
            HOperatorSet.GenEmptyObj(out ho_RegionClosing);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp1);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            hv_Row = new HTuple();
            hv_Column = new HTuple();
            hv_Radius = new HTuple();
            try
            {
                try
                {
                    ho_ImageReduced.Dispose();
                    HOperatorSet.ReduceDomain(ho_Image, ho_Region_COPY_INP_TMP, out ho_ImageReduced
                        );
                    ho_Region_COPY_INP_TMP.Dispose();
                    HOperatorSet.Threshold(ho_ImageReduced, out ho_Region_COPY_INP_TMP, 120,
                        255);
                    ho_RegionFillUp.Dispose();
                    HOperatorSet.FillUp(ho_Region_COPY_INP_TMP, out ho_RegionFillUp);
                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_ImageReduced, out ho_Region1, 0, 120);
                    ho_RegionIntersection.Dispose();
                    HOperatorSet.Intersection(ho_Region1, ho_RegionFillUp, out ho_RegionIntersection
                        );
                    ho_RegionClosing.Dispose();
                    HOperatorSet.ClosingCircle(ho_RegionIntersection, out ho_RegionClosing, 3.5);
                    ho_RegionFillUp1.Dispose();
                    HOperatorSet.FillUp(ho_RegionClosing, out ho_RegionFillUp1);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_RegionFillUp1, out ho_ConnectedRegions);
                    ho_Region_COPY_INP_TMP.Dispose();
                    HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_Region_COPY_INP_TMP,
                        (new HTuple("area")).TupleConcat("circularity"), "and", (new HTuple(200)).TupleConcat(
                        0.9), (new HTuple("max")).TupleConcat(1));
                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Radius.Dispose();
                    HOperatorSet.SmallestCircle(ho_Region_COPY_INP_TMP, out hv_Row, out hv_Column,
                        out hv_Radius);
                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    hv_Index1.Dispose();
                    HOperatorSet.AddMetrologyObjectCircleMeasure(hv_MetrologyHandle, hv_Row,
                        hv_Column, hv_Radius, hv_Measure_Height, 3, 1, hv_Measure_Threshold,
                        new HTuple(), new HTuple(), out hv_Index1);

                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "num_measures",
                        hv_Measure_Num);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "min_score",
                        0.3);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_select",
                        "first");
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_transition",
                        "all");

                    HOperatorSet.ApplyMetrologyModel(ho_ImageReduced, hv_MetrologyHandle);
                    ho_MeeasureRegion.Dispose(); hv_Row1.Dispose(); hv_Column1.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_MeeasureRegion, hv_MetrologyHandle,
                        "all", "all", out hv_Row1, out hv_Column1);
                    ho_Cross.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross, hv_Row1, hv_Column1, 6, 0.785398);
                    ho_Contour.Dispose();
                    HOperatorSet.GetMetrologyObjectResultContour(out ho_Contour, hv_MetrologyHandle,
                        "all", "all", 1.5);
                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Radius.Dispose();
                    HOperatorSet.SmallestCircleXld(ho_Contour, out hv_Row, out hv_Column, out hv_Radius);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);

                }

                ho_Region_COPY_INP_TMP.Dispose();
                ho_ImageReduced.Dispose();
                ho_RegionFillUp.Dispose();
                ho_Region1.Dispose();
                ho_RegionIntersection.Dispose();
                ho_RegionClosing.Dispose();
                ho_RegionFillUp1.Dispose();
                ho_ConnectedRegions.Dispose();

                hv_MetrologyHandle.Dispose();
                hv_Index1.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Region_COPY_INP_TMP.Dispose();
                ho_ImageReduced.Dispose();
                ho_RegionFillUp.Dispose();
                ho_Region1.Dispose();
                ho_RegionIntersection.Dispose();
                ho_RegionClosing.Dispose();
                ho_RegionFillUp1.Dispose();
                ho_ConnectedRegions.Dispose();

                hv_MetrologyHandle.Dispose();
                hv_Index1.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }

        #endregion
    }
}
