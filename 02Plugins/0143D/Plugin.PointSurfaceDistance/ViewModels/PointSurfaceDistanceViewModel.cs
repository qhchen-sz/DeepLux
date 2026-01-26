using EventMgrLib;
using HalconDotNet;
using Plugin.PointSurfaceDistance.Views;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using HandyControl.Controls;

namespace Plugin.PointSurfaceDistance.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Rect1X1,
        Rect1Y1,
        Rect1X2,
        Rect1Y2,
        Rect2X1,
        Rect2Y1,
        Rect2X2,
        Rect2Y2
    }

    #endregion

    [Category("3D")]
    [DisplayName("点面距离")]
    [ModuleImageName("PointSurfaceDistance")]
    [Serializable]
    public class PointSurfaceDistanceViewModel : ModuleBase
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
                if(!IsManMual)
                    GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                OutRegion.GenRectangle1(
                           Convert.ToDouble(GetLinkValue(Rect1Y1)),
                           Convert.ToDouble(GetLinkValue(Rect1X1)),
                           Convert.ToDouble(GetLinkValue(Rect1Y2)),
                           Convert.ToDouble(GetLinkValue(Rect1X2)));
                GetHomMat2D();
                if (HomMat2D != null && HomMat2D.Length > 0)
                {
                    OutRegion = OutRegion.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");

                }
                int num = DispImage.CountChannels();
                var type = DispImage.GetImageType();

                if (DispImage.Type == "3D")
                {

                    //HObject Contours = new HObject(), Line = new HObject(), Cross = new HObject();
                    //HTuple Distance = -999;
                    //if (num == 2)
                    //{
                    //    HImage HeightImage = DispImage.Decompose2(out HImage Grayimage);
                    //    Gap.Creat_Contour(OutRegion, HeightImage, out  Contours, out  Line, out  Cross, Offset, out  Distance);
                        
                    //}
                    //else if(num == 1) 
                    //{
                    //    Gap.Creat_Contour(OutRegion, DispImage, out  Contours, out  Line, out  Cross, Offset, out  Distance);
                    //}
                    //if ((double)Distance == -999.0)
                    //{
                    //    ChangeModuleRunStatus(eRunStatus.NG);
                    //    GapValue = (double)Distance;
                    //    return false;
                    //}
                    //GapValue = (double)Distance;
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测点, "red", new HObject(Cross)));
                    //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "red", new HObject(Contours)));
                    //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "red", new HObject(Line)));
                    //ShowHRoi();
                    //InitRect1Method(true);
                    //InitRect2Method(true);

                }
                else
                {

                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }




                var view = ModuleView as PointSurfaceDistanceView;
                VMHWindowControl mWindowH;
                if (view == null || view.IsClosed)
                {
                    mWindowH = ViewDic.GetView(DispImage.DispViewID);
                }
                else
                {
                    mWindowH = view.mWindowH;
                }




                ChangeModuleRunStatus(eRunStatus.OK);
                return true;




                //if (DispImage != null && DispImage.IsInitialized())
                //{
                //    GetHomMat2D();
                //    if (DisenableAffine2d && HomMat2D_Inverse!=null && HomMat2D_Inverse.Length > 0)
                //    {
                //        DisenableAffine2d = false;
                //        Aff.Affine2d(HomMat2D_Inverse, TempRectangle1, InitRectangle1);
                //        if (InitLineChanged_Flag)
                //        {
                //            InitLineStartX.Text = InitLine.StartX.ToString();
                //            InitLineStartY.Text = InitLine.StartY.ToString();
                //            InitLineEndX.Text = InitLine.EndX.ToString();
                //            InitLineEndY.Text = InitLine.EndY.ToString();
                //        }
                //    }
                //    if (HomMat2D != null && HomMat2D.Length > 0)
                //    {
                //        InitLine.StartX = Convert.ToDouble(GetLinkValue(InitLineStartX));
                //        InitLine.StartY = Convert.ToDouble(GetLinkValue(InitLineStartY));
                //        InitLine.EndX = Convert.ToDouble(GetLinkValue(InitLineEndX));
                //        InitLine.EndY = Convert.ToDouble(GetLinkValue(InitLineEndY));
                //        Aff.Affine2d(HomMat2D, InitLine, TranLine);
                //    }
                //    else
                //    {
                //        InitLine.StartX = TranLine.StartX = TempLine.StartX;
                //        InitLine.StartY = TranLine.StartY = TempLine.StartY;
                //        InitLine.EndX = TranLine.EndX = TempLine.EndX;
                //        InitLine.EndY = TranLine.EndY = TempLine.EndY;
                //    }
                //    Meas.MeasLine(DispImage, TranLine, MeasInfo, OutLine, out HTuple RowList, out HTuple ColList, out HXLDCont m_MeasXLD, null);
                //    if (ShowResultPoint && RowList.ToDArr().Length > 0) //显示结果点
                //    {
                //        Gen.GenCross(out HObject m_MeasCross, RowList, ColList, MeasInfo.Length2, new HTuple(45).TupleRad());
                //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测点, "red", new HObject(m_MeasCross)));
                //    }
                //    if (ShowResultLine && RowList.ToDArr().Length > 0) //显示结果线
                //    {
                //        Gen.GenContour(out HObject m_ResultXLD, OutLine.StartY, OutLine.EndY, OutLine.StartX, OutLine.EndX);
                //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", new HObject(m_ResultXLD)));
                //    }
                //    if (ShowMeasContour) //显示检测范围
                //    {
                //        if (m_MeasXLD != null && m_MeasXLD.IsInitialized())
                //        {
                //            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测范围, "blue", new HObject(m_MeasXLD.GenRegionContourXld("margin").Union1().ShapeTrans("rectangle2"))));
                //        }
                //    }
                //    ShowHRoi();
                //    if (RowList.ToDArr().Length > 0)
                //    {
                //        OutLine.Status = true;
                //        ChangeModuleRunStatus(eRunStatus.OK);
                //        return true;
                //    }
                //    else
                //    {
                //        OutLine.Status = false;
                //        ChangeModuleRunStatus(eRunStatus.NG);
                //        return false;
                //    }
                //}
                //else
                //{
                //    ChangeModuleRunStatus(eRunStatus.NG);
                //    return false;
                //}
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
            //OutLine.Status == eRunStatus.OK ? true : false; 
            
            AddOutputParam("缝隙宽度", "double", 0);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop
        private bool  IsLoad = false;
        private bool IsManMual = true;
        private bool DisenableAffine2d = false;
        private bool InitLineChanged_Flag = false;
        private bool _ShowResultPoint = true;
        HRegion OutRegion = new HRegion(0.0, 0, 3);
        /// <summary>显示结果点</summary>
        public bool ShowResultPoint
        {
            get { return _ShowResultPoint; }
            set { Set(ref _ShowResultPoint, value); }
        }
        private bool _ShowMeasContour = true;
        private int _Offset = 5;
        private double _GapValue = 0;
        public int Offset
        {
            get { return _Offset; }
            set { _Offset = value; RaisePropertyChanged(); }
        }
        public double GapValue
        {
            get { return _GapValue; }
            set { _GapValue = value; RaisePropertyChanged(); }
        }
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

        /// <summary>
        /// 变换前-Rect
        /// </summary>
        private LinkVarModel _Rect1X1 = new LinkVarModel() { Text = "10" };
        public LinkVarModel Rect1X1
        {
            get { return _Rect1X1; }
            set { _Rect1X1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1Y1 = new LinkVarModel() { Text = "10" };
        public LinkVarModel Rect1Y1
        {
            get { return _Rect1Y1; }
            set { _Rect1Y1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1X2 = new LinkVarModel() { Text = "10" };
        public LinkVarModel Rect1X2
        {
            get { return _Rect1X2; }
            set { _Rect1X2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1Y2 = new LinkVarModel() { Text = "10" };
        public LinkVarModel Rect1Y2
        {
            get { return _Rect1Y2; }
            set { _Rect1Y2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect2X1 = new LinkVarModel() { Text = "20" };
        public LinkVarModel Rect2X1
        {
            get { return _Rect2X1; }
            set { _Rect2X1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect2Y1 = new LinkVarModel() { Text = "20" };
        public LinkVarModel Rect2Y1
        {
            get { return _Rect2Y1; }
            set { _Rect2Y1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect2X2 = new LinkVarModel() { Text = "30" };
        public LinkVarModel Rect2X2
        {
            get { return _Rect2X2; }
            set { _Rect2X2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect2Y2 = new LinkVarModel() { Text = "30" };
        public LinkVarModel Rect2Y2
        {
            get { return _Rect2Y2; }
            set { _Rect2Y2 = value; RaisePropertyChanged(); }
        }
        /// <summary>
        /// 变换前-直线信息
        /// </summary>
        public ROIRectangle1 InitRectangle1 { get; set; } = new ROIRectangle1();
        public ROIRectangle1 InitRectangle2 { get; set; } = new ROIRectangle1();
        /// <summary>
        /// 直线信息
        /// </summary>
        public ROIRectangle1 TempRectangle1 { get; set; } = new ROIRectangle1();
        /// <summary>
        /// 变换后-直线信息
        /// </summary>
        public ROIRectangle1 TranRectangle1 { get; set; } = new ROIRectangle1();
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
                    if (!IsLoad)
                    {
                        InitRect1Method(true);
                        InitRect2Method(true);
                    }
                        
                }
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            IsLoad = true;
            base.Loaded();
            var view = ModuleView as PointSurfaceDistanceView;
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
                    ShowHText();
                    //InitRect1Method();

                    //InitRect2Method();
                }
                Rect1X1.TextChanged = new Action(() => { InitRect1Changed(); });
                //Rect1X2.TextChanged = new Action(() => { InitRect1Changed(); });
                Rect1Y1.TextChanged = new Action(() => { InitRect1Changed(); });
                //Rect1Y2.TextChanged = new Action(() => { InitRect1Changed(); });

                //Rect2X1.TextChanged = new Action(() => { InitRect2Changed(); });
                //Rect2X2.TextChanged = new Action(() => { InitRect2Changed(); });
                //Rect2Y1.TextChanged = new Action(() => { InitRect2Changed(); });
                //Rect2Y2.TextChanged = new Action(() => { InitRect2Changed(); });
            }
            IsLoad = false;
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.Rect1X1:
                    Rect1X1.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect1Y1:
                    Rect1Y1.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect1X2:
                    Rect1X2.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect1Y2:
                    Rect1Y2.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect2X1:
                    Rect2X1.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect2Y1:
                    Rect2Y1.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect2X2:
                    Rect2X2.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect2Y2:
                    Rect2Y2.Text = obj.LinkName;
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
                            case eLinkCommand.Rect1X1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1X1");
                                break;
                            case eLinkCommand.Rect1Y1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Y1");
                                break;
                            case eLinkCommand.Rect1X2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1X2");
                                break;
                            case eLinkCommand.Rect1Y2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Y2");
                                break;
                            case eLinkCommand.Rect2X1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2X1");
                                break;
                            case eLinkCommand.Rect2Y1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2Y1");
                                break;
                            case eLinkCommand.Rect2X2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2X2");
                                break;
                            case eLinkCommand.Rect2Y2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2Y2");
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
                        IsManMual = true;
                        ExeModule();
                        IsManMual = false;
                        //InitLineMethod();
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
                        var view = this.ModuleView as PointSurfaceDistanceView;
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
        public override void ShowHRoi()
        {
            var view = ModuleView as PointSurfaceDistanceView;
            VMHWindowControl mWindowH;
            bool dispSearchRegion = true;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispSearchRegion = false;
            }
            else
            {
                mWindowH = view.mWindowH;
                if (mWindowH != null)
                {
                    mWindowH.ClearWindow();
                    mWindowH.Image = new RImage(DispImage);
                }
            }
            if (dispSearchRegion)
            {
                if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle1))
                {
                    ROIRectangle1 ROIRect1 = (ROIRectangle1)
                        RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle1];
                    mWindowH.WindowH.genRect1(
                        ModuleParam.ModuleName + ROIDefine.Rectangle1,
                        ROIRect1.row1,
                        ROIRect1.col1,
                        ROIRect1.row2,
                        ROIRect1.col2,
                        ref RoiList
                    );
                }
                else
                {
                    mWindowH.WindowH.genRect1(
                        ModuleParam.ModuleName + ROIDefine.Rectangle1,
                        5,
                        5,
                        mWindowH.hv_imageHeight - 5,
                        mWindowH.hv_imageWidth - 5,
                        ref RoiList
                    );
                }
            }
            List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            foreach (HRoi roi in roiList)
            {
                if (roi.roiType == HRoiType.文字显示)
                {
                    HText roiText = (HText)roi;
                    ShowTool.SetFont(
                        mWindowH.hControl.HalconWindow,
                        roiText.size,
                        "false",
                        "false"
                    );
                    ShowTool.SetMsg(
                        mWindowH.hControl.HalconWindow,
                        roiText.text,
                        "image",
                        roiText.row,
                        roiText.col,
                        roiText.drawColor,
                        "false"
                    );
                }
                else if (roi.roiType == HRoiType.搜索范围)
                {
                    //if (ShowSearchRegion && ModuleView == null)
                    //{
                    //    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                    //}
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                }
            }
        }
        private void ShowHText()
        {
            var view = ModuleView as PointSurfaceDistanceView;
            if (view == null)
                return;
            if (RoiList.Count == 0 || DispImage == null)
                return;
            HTuple info = RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle1].GetModelData();
            Rect1Y1.Text = Math.Round(info.DArr[0], 0).ToString();
            Rect1X1.Text = Math.Round(info.DArr[1], 0).ToString();
            Rect1Y2.Text = Math.Round(info.DArr[2], 0).ToString();
            Rect1X2.Text = Math.Round(info.DArr[3], 0).ToString();
            if (
                info.DArr[2] > view.mWindowH.hv_imageHeight
                || info.DArr[3] > view.mWindowH.hv_imageWidth
            )
            {
                ROIRectangle1 ROIRect1 = new ROIRectangle1(
                    view.mWindowH.hv_imageHeight/2,
                    view.mWindowH.hv_imageWidth /2,
                    view.mWindowH.hv_imageHeight /3,
                    view.mWindowH.hv_imageWidth /3
                );
                RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle1] = ROIRect1;
            }
            ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
                ShowTool.SetMsg(
                    view.mWindowH.hControl.HalconWindow,
                    "搜索框",
                    "image",
                    0,
                    0,
                    "red",
                    "false"
                );
            

        }
        private void InitRect1Changed()
        {
            if (InitLineChanged_Flag == true) return;
            InitRectangle1.row1 = Convert.ToDouble(GetLinkValue(Rect1Y1));
            InitRectangle1.col1 = Convert.ToDouble(GetLinkValue(Rect1X1));
            InitRectangle1.row2 = Convert.ToDouble(GetLinkValue(Rect1Y2));
            InitRectangle1.col2 = Convert.ToDouble(GetLinkValue(Rect1X2));
            var view = ModuleView as PointSurfaceDistanceView;


            ShowHRoi();
            ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
            ShowTool.SetMsg(
       view.mWindowH.hControl.HalconWindow,
       "点",
       "image",
       InitRectangle1.row1 + 5,
       InitRectangle1.col1 + 5,
       "cyan",
       "false"
   );
            //DisenableAffine2d = true;
            //if (roiLine != null)
            //{
            //    if (DisenableAffine2d && HomMat2D != null && HomMat2D.Length > 0)
            //    {
            //        Aff.Affine2d(HomMat2D, InitLine, TempLine);
            //        if (InitLineChanged_Flag)
            //        {
            //            roiLine.StartX = TempLine.StartX;
            //            roiLine.StartY = TempLine.StartY;
            //            roiLine.EndX = TempLine.EndX;
            //            roiLine.EndY = TempLine.EndY;
            //        }
            //    }
            //    else
            //    {
            //        roiLine.StartX = InitLine.StartX;
            //        roiLine.StartY = InitLine.StartY;
            //        roiLine.EndX = InitLine.EndX;
            //        roiLine.EndY = InitLine.EndY;
            //        TempLine.StartX = InitLine.StartX;
            //        TempLine.StartY = InitLine.StartY;
            //        TempLine.EndX = InitLine.EndX;
            //        TempLine.EndY = InitLine.EndY;
            //    }
            //    ExeModule();
            //    InitLineMethod();
            //}
        }
        private void InitRect2Changed()
        {
            if (InitLineChanged_Flag == true) return;

            InitRectangle2.row1 = Convert.ToDouble(GetLinkValue(Rect2Y1));
            InitRectangle2.col1 = Convert.ToDouble(GetLinkValue(Rect2X1));
            InitRectangle2.row2 = Convert.ToDouble(GetLinkValue(Rect2Y2));
            InitRectangle2.col2 = Convert.ToDouble(GetLinkValue(Rect2X2));

            var view = ModuleView as PointSurfaceDistanceView;
            ShowTool.SetMsg(
       view.mWindowH.hControl.HalconWindow,
       "面",
       "image",
       InitRectangle2.row1 + 5,
       InitRectangle2.col1 + 5,
       "cyan",
       "false"
   );
            ShowHRoi();
            //DisenableAffine2d = true;
            //if (roiLine != null)
            //{
            //    if (DisenableAffine2d && HomMat2D != null && HomMat2D.Length > 0)
            //    {
            //        Aff.Affine2d(HomMat2D, InitLine, TempLine);
            //        if (InitLineChanged_Flag)
            //        {
            //            roiLine.StartX = TempLine.StartX;
            //            roiLine.StartY = TempLine.StartY;
            //            roiLine.EndX = TempLine.EndX;
            //            roiLine.EndY = TempLine.EndY;
            //        }
            //    }
            //    else
            //    {
            //        roiLine.StartX = InitLine.StartX;
            //        roiLine.StartY = InitLine.StartY;
            //        roiLine.EndX = InitLine.EndX;
            //        roiLine.EndY = InitLine.EndY;
            //        TempLine.StartX = InitLine.StartX;
            //        TempLine.StartY = InitLine.StartY;
            //        TempLine.EndX = InitLine.EndX;
            //        TempLine.EndY = InitLine.EndY;
            //    }
            //    ExeModule();
            //    InitLineMethod();
            //}
        }
        ROILine roiLine;
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as PointSurfaceDistanceView;
                if (view == null) return;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length < 1) return;
                ROIRectangle1 Rect1 = new ROIRectangle1();
                RoiList[index] = roi;
                
                    ROIRectangle1 rectangle1 = (ROIRectangle1)roi;
                    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                    {
                        HRegion region = rectangle1.GetRegion();
                        region = region.AffineTransRegion(new HHomMat2D(HomMat2D_Inverse), "nearest_neighbor");
                        region.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
                        Rect1.row1 = Math.Round((double)row1, 3);
                        Rect1.col1 = Math.Round((double)column1, 3);
                        Rect1.row2 = Math.Round((double)row2, 3);
                        Rect1.col2 = Math.Round((double)column2, 3);
                    
                    }
                    else
                    {
                        Rect1.row1 = Math.Round(rectangle1.row1, 3);
                        Rect1.col1 = Math.Round(rectangle1.col1, 3);
                        Rect1.row2 = Math.Round(rectangle1.row2, 3);
                        Rect1.col2 = Math.Round(rectangle1.col2, 3);
                        
                    }
                    if (!Rect1X1.Text.StartsWith("&"))
                    {
                        Rect1X1.Text = Rect1.col1.ToString();
                    }
                    if (!Rect1Y1.Text.StartsWith("&"))
                    {
                    Rect1Y1.Text = Rect1.row1.ToString();
                    }
                    if (!Rect1X2.Text.StartsWith("&"))
                    {
                    Rect1X2.Text = Rect1.col2.ToString();
                    }
                    if (!Rect1Y2.Text.StartsWith("&"))
                    {
                    Rect1Y2.Text = Rect1.row2.ToString();
                    }


                    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, rectangle1.row1, rectangle1.col1, rectangle1.row2, rectangle1.col2, ref RoiList);

                IsManMual = true;
                ExeModule();
                IsManMual = false;
                //roiLine = roi as ROILine;
                //    if (roiLine != null)
                //    {
                //        TempLine.StartX = Math.Round(roiLine.StartX, 3);
                //        TempLine.StartY = Math.Round(roiLine.StartY, 3);
                //        TempLine.EndX = Math.Round(roiLine.EndX, 3);
                //        TempLine.EndY = Math.Round(roiLine.EndY, 3);
                //        DisenableAffine2d = true;
                //        InitLineChanged_Flag = true;
                //        ExeModule();
                //        InitLineMethod();
                //        InitLineChanged_Flag = false;
                //   }

            }
            catch (Exception ex)
            {
            }
        }
        public void InitRect1Method(bool isDisp = false)
        {
            var view = ModuleView as PointSurfaceDistanceView;
            
            if (view == null) return;
            VMHWindowControl mWindowH;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
            }
            ClearRoiAndText();
            if(!isDisp)
                mWindowH.ClearROI();
            
            if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle1))
            {
                ROIRectangle1 ROIRect1 = (ROIRectangle1)RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle1];
                if ( HomMat2D != null && HomMat2D.Length > 0)
                {
                    HRegion region = new HRegion();
                    region.GenRectangle1(
                        Convert.ToDouble(GetLinkValue(Rect1Y1)),
                        Convert.ToDouble(GetLinkValue(Rect1X1)),
                        Convert.ToDouble(GetLinkValue(Rect1X2)),
                        Convert.ToDouble(GetLinkValue(Rect1Y2)));
                    region = region.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                    region.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
                    //ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
                    if (region.Area.I != 0)
                    {
                        mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1,
                          row1,
                          column1,
                          row2,
                          column2,
                          ref RoiList);
                        ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
                        ShowTool.SetMsg(
                        view.mWindowH.hControl.HalconWindow,
                        "点",
                        "image",
                        row1 + 5,
                        column1 + 5,
                        "cyan",
                        "false"
                        );
  
                    }
                    else
                    {
                        //mWindowH.WindowH.genRect2(ModuleParam.ModuleName + ROIDefine.Rectangle2, 200, 200, 0, 30, 30, ref RoiList);
                    }
                }
                else
                {

                    mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, Convert.ToDouble(GetLinkValue(Rect1Y1)), Convert.ToDouble(GetLinkValue(Rect1X1)), Convert.ToDouble(GetLinkValue(Rect1Y2)), Convert.ToDouble(GetLinkValue(Rect1X2)), ref RoiList);
                    ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
                    ShowTool.SetMsg(
                view.mWindowH.hControl.HalconWindow,
                "点",
                "image",
                Convert.ToDouble(GetLinkValue(Rect1Y1)) + 5,
                Convert.ToDouble(GetLinkValue(Rect1X1)) + 5,
                "cyan",
                "false"
                );
                }
            }
            else
            {

                //mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, 200, 200, 230, 230, ref RoiList);
                ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
                ShowTool.SetMsg(
                view.mWindowH.hControl.HalconWindow,
                "点",
                "image",
                200 + 5,
                200 + 5,
                "cyan",
                "false"
                );
            }

            //if (TranLine.FlagLineStyle != null)
            //{
            //    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, TranLine.StartX, TranLine.StartY, TranLine.EndX, TranLine.EndY, ref RoiList);
            //}
            //else if (DispImage != null && !RoiList.ContainsKey(ModuleParam.ModuleName))
            //{
            //    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageWidth / 2, ref RoiList);
            //    TranLine.StartX = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.StartY = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.EndX = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.EndY = view.mWindowH.hv_imageWidth / 4;
            //}
            //else if (DispImage != null && RoiList.ContainsKey(ModuleParam.ModuleName))
            //{
            //    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
            //    {
            //        view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, TranLine.StartY, TranLine.StartX, TranLine.EndY, TranLine.EndX, ref RoiList);
            //        Aff.Affine2d(HomMat2D_Inverse, TranLine, InitLine);
            //        InitLine.StartX = Math.Round(InitLine.StartX, 3);
            //        InitLine.StartY = Math.Round(InitLine.StartY, 3);
            //        InitLine.EndX = Math.Round(InitLine.EndX, 3);
            //        InitLine.EndY = Math.Round(InitLine.EndY, 3);
            //        if (InitLineChanged_Flag)
            //        {
            //            InitLineStartX.Text = InitLine.StartX.ToString();
            //            InitLineStartY.Text = InitLine.StartY.ToString();
            //            InitLineEndX.Text = InitLine.EndX.ToString();
            //            InitLineEndY.Text = InitLine.EndY.ToString();
            //        }
            //    }
            //    else
            //    {
            //        view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, InitLine.StartY, InitLine.StartX, InitLine.EndY, InitLine.EndX, ref RoiList);
            //        if (InitLineChanged_Flag)
            //        {
            //            InitLineStartX.Text = InitLine.StartX.ToString();
            //            InitLineStartY.Text = InitLine.StartY.ToString();
            //            InitLineEndX.Text = InitLine.EndX.ToString();
            //            InitLineEndY.Text = InitLine.EndY.ToString();
            //        }
            //    }
            //}
        }

        public void InitRect2Method(bool isDisp = false)
        {
            var view = ModuleView as PointSurfaceDistanceView;
            if (view == null) return;
            VMHWindowControl mWindowH;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
            }
            //ClearRoiAndText();
            if (!isDisp)
                mWindowH.ClearROI();
            if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle1))
            {
                ROIRectangle1 ROIRect1 = (ROIRectangle1)RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle1];
                if (HomMat2D != null && HomMat2D.Length > 0)
                {
                    HRegion region = new HRegion();
                    region.GenRectangle1(
                        Convert.ToDouble(GetLinkValue(Rect2Y1)),
                        Convert.ToDouble(GetLinkValue(Rect2X1)),
                        Convert.ToDouble(GetLinkValue(Rect2X2)),
                        Convert.ToDouble(GetLinkValue(Rect2Y2)));
                    region = region.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                    region.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
                    if (region.Area.I != 0)
                    {
                        ShowTool.SetMsg(
                        view.mWindowH.hControl.HalconWindow,
                        "面",
                        "image",
                        row1 + 5,
                        column1 + 5,
                        "cyan",
                        "false"
                        );
                        mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1,
                            row1,
                            column1,
                            row2,
                            column2,
                            ref RoiList);
                    }
                    else
                    {
                        //mWindowH.WindowH.genRect2(ModuleParam.ModuleName + ROIDefine.Rectangle2, 200, 200, 0, 30, 30, ref RoiList);
                    }
                }
                else
                {
                    ShowTool.SetMsg(
                    view.mWindowH.hControl.HalconWindow,
                    "面",
                    "image",
                    Convert.ToDouble(GetLinkValue(Rect2Y1)) + 5,
                    Convert.ToDouble(GetLinkValue(Rect2X1)) + 5,
                    "cyan",
                    "false"
                    );
                    mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, Convert.ToDouble(GetLinkValue(Rect2Y1)), Convert.ToDouble(GetLinkValue(Rect2X1)), Convert.ToDouble(GetLinkValue(Rect2Y2)), Convert.ToDouble(GetLinkValue(Rect2X2)), ref RoiList);
                }
            }
            else
            {
                                ShowTool.SetMsg(
                view.mWindowH.hControl.HalconWindow,
                "面",
                "image",
                300 + 5,
                300 + 5,
                "cyan",
                "false"
                );
                mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, 300, 300, 330, 330, ref RoiList);
            }

            //if (TranLine.FlagLineStyle != null)
            //{
            //    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, TranLine.StartX, TranLine.StartY, TranLine.EndX, TranLine.EndY, ref RoiList);
            //}
            //else if (DispImage != null && !RoiList.ContainsKey(ModuleParam.ModuleName))
            //{
            //    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageWidth / 2, ref RoiList);
            //    TranLine.StartX = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.StartY = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.EndX = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.EndY = view.mWindowH.hv_imageWidth / 4;
            //}
            //else if (DispImage != null && RoiList.ContainsKey(ModuleParam.ModuleName))
            //{
            //    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
            //    {
            //        view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, TranLine.StartY, TranLine.StartX, TranLine.EndY, TranLine.EndX, ref RoiList);
            //        Aff.Affine2d(HomMat2D_Inverse, TranLine, InitLine);
            //        InitLine.StartX = Math.Round(InitLine.StartX, 3);
            //        InitLine.StartY = Math.Round(InitLine.StartY, 3);
            //        InitLine.EndX = Math.Round(InitLine.EndX, 3);
            //        InitLine.EndY = Math.Round(InitLine.EndY, 3);
            //        if (InitLineChanged_Flag)
            //        {
            //            InitLineStartX.Text = InitLine.StartX.ToString();
            //            InitLineStartY.Text = InitLine.StartY.ToString();
            //            InitLineEndX.Text = InitLine.EndX.ToString();
            //            InitLineEndY.Text = InitLine.EndY.ToString();
            //        }
            //    }
            //    else
            //    {
            //        view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, InitLine.StartY, InitLine.StartX, InitLine.EndY, InitLine.EndX, ref RoiList);
            //        if (InitLineChanged_Flag)
            //        {
            //            InitLineStartX.Text = InitLine.StartX.ToString();
            //            InitLineStartY.Text = InitLine.StartY.ToString();
            //            InitLineEndX.Text = InitLine.EndX.ToString();
            //            InitLineEndY.Text = InitLine.EndY.ToString();
            //        }
            //    }
            //}
        }
        #endregion
    }
}
