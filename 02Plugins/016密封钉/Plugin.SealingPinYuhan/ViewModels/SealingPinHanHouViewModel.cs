using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.SealingPinYuhan.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using System.Windows.Media;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views.Dock;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using EVDll;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Plugin.SealingPinYuhan.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum,
        CenterY,
        Scale,
    }
    public enum eOperateCommand
    {
        StartLearn,
        Edit,
        EndLearn,
        Cancel
    }
    public enum eLocationClass
    {
        Right,
        Left,
        Center
    }
    public enum eAiColor
    {
        red=0,
        blue = 1,
        green =2,
        cyan = 3,
        yellow = 4,
        coral = 5,
        orange = 6,
        pink = 7,

    }
    #endregion

    [Category("密封钉")]
    [DisplayName("密封钉预焊算法")]
    [ModuleImageName("SealingPinYuhan")]
    [Serializable]
    public class SealingPinYuhanViewModel : ModuleBase
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
                GetDispImage(InputImageLinkText);
                string erro = "";
                if (ClassAi == null || !ClassAi.IsRunning)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                SealingPinYuhanViewModel aiViewModel = this;
                if (OutRegion == null)
                    OutRegion = new HRegion();
                OutRegion.GenEmptyObj();
                HImage Result = ClassAi.AiRun(DispImage, ref erro, this.CONF_THRESHOLD, this.NMS_THRESHOLD);
               
                if (IsDispAiRegion)
                {
                    HRegion region1 = new HRegion();
                    HRegion region2 = new HRegion();
                    //HRegion region3 = new HRegion();
                    //HRegion region4 = new HRegion();
                    region1 = Result.Threshold(1.0, 1.0);
                    region2 = Result.Threshold(2.0, 2.0);
                    //region3 = Result.Threshold(3.0, 3.0);
                    //region4 = Result.Threshold(4.0, 4.0);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(region1),true));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai2", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(region2), true));
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai3", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)4).ToString(), new HObject(region3), true));
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai4", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)5).ToString(), new HObject(region4), true));
                }





                //OutputImage.MinMaxGray(OutputImage, 0, out HTuple Min, out HTuple Max, out HTuple Range);
                Algorithm.mfdyuhan(Result, out HObject ho_Circle, out HTuple hv_Row1,
       out HTuple hv_Column1, out HTuple hv_Radius, out HTuple hv_Area2);
                    OutSideCircleX = 0;
                    OutSideCircleY = 0;
                    InSideCircleX = 0;
                    InSideCircleY = 0;

                double.TryParse(GetLinkValue(Scale).ToString(), out double SS);
                //double SS = Convert.ToDouble(GetLinkValue(Scale));
                OutSideCircleX = Math.Round((double)(hv_Column1), 3);
                    OutSideCircleY = Math.Round((double)(hv_Row1), 3); 
                    InSideCircleX = Math.Round((double)(hv_Radius ), 3);
                    InSideCircleY = Math.Round((double)(hv_Area2 * SS), 3);
                HRegion region3 = new HRegion();
                region3.GenCircle(hv_Row1, hv_Column1, hv_Radius);
                region3 = region3.ErosionCircle(14.0);
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai4", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)5).ToString(), new HObject(region3)));
                //else if(SelectLocation.ToString() == "Right")
                //{
                //ResultValue3 = Math.Round((double)Distance2[0], 2);
                //ResultValue5 = Math.Round((double)Distance1[0], 2);
                //}
                //else
                //{
                //ResultValue2 = Math.Round((double)Distance2[0], 2);
                // ResultValue4 = Math.Round((double)Distance1[0], 2);
                //if (Distance2.Length> 1)
                //{
                // ResultValue3 = Math.Round((double)Distance2[1], 2);
                //ResultValue5 = Math.Round((double)Distance1[1], 2);
                //}


                if (IsCross)
                {
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 1, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.red.ToString(), new HObject(Cross)));
                  
                }
                if (IsFindRegions)
                {
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 2, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(FindRegions)));
                   
                }
                if (IsFindCircles)
                {

                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 3, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.green.ToString(), new HObject(Arrow)));
                }

                //var temp = Max.ToString();
                //int Count = DefectNum;//种类个数
                //HRegion outregion = new HRegion();

                //outregion.GenEmptyObj();


                //for (int i = 0; i < Count; i++)
                //{
                //    outregion = aiViewModel.OutputImage.Threshold(i + 1.0, i + 1.0);
                //    OutRegion = OutRegion.ConcatObj(outregion);


                //    //string dispstr = "类别:" + i+1 ;
                //    var area = outregion.AreaCenter(out double Row, out double Column);
                //    if (area != 0)
                //    {
                //        string dispstr = "类别:" + (i + 1);
                //        eAiColor color = (eAiColor)i;
                //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName+i, ModuleParam.Remarks, HRoiType.检测结果, color.ToString(), new HObject(OutRegion)));
                //        ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName + i, ModuleParam.Remarks, HRoiType.文字显示, color.ToString(), dispstr, Column, Row, 16));
                //    }

                //}
                //var numm = outregion.CountObj();
                //AddOutputParam("区域" , "HRegion", OutRegion);
                //OutRegion.Connection();

                ShowHRoi();
                
                //else if(Result is ResultListResult listResult)//输出数据
                //{
                //    HObject roi = new HObject();
                //    Type = listResult.ResultList.Id[0];
                //    roi.GenEmptyObj();
                //    for (int i = 0; i < listResult.ResultList.Id.Count; i++)
                //    {
                //        if(ClassAi.eAi == eAiClass.GPU目标检测新)
                //        {
                //            HOperatorSet.GenRectangle2(out HObject rectangle, listResult.ResultList.Y[i], listResult.ResultList.X[i], listResult.ResultList.Angel[i], listResult.ResultList.Length1[i], listResult.ResultList.Length2[i]);
                //            //AddOutputParam("区域" + (i + 1), "HRegion", rectangle);
                //            HOperatorSet.ConcatObj(roi, rectangle, out roi);
                //            rectangle.Dispose();
                //        }
                //        //Type.Add(listResult.ResultList.Id[i]);
                //        string dispstr = "类别:" + listResult.ResultList.Id[i] + "得分:" + listResult.ResultList.Score[i].ToString("f2");
                //        ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName+i, ModuleParam.Remarks, HRoiType.文字显示, "red", dispstr, listResult.ResultList.X[i],listResult.ResultList.Y[i],16));
                        
                //    }
                //    HOperatorSet.CountObj(roi, out HTuple num);
                    
                //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "red", new HObject(roi)));
                //    ShowHRoi();
                //    roi.Dispose();
                //}


                ChangeModuleRunStatus(eRunStatus.OK);
                if(OutputImage!=null)
                    OutputImage.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                OutRegion.Dispose();
                if (OutputImage != null)
                    OutputImage.Dispose();
                return false;
            }
            finally
            {
                OutRegion?.Dispose();
                OutputImage?.Dispose();
            }
        }
        public override void InitModule()
        {
            base.Init();
            DeviceStatic = false;
            if (IsOpen)
            {

                string erro = "";
                if (ClassAi == null)
                    ClassAi = new AI();

                if (ClassAi.IsRunning)
                {
                    ClassAi.AiClose(ref erro);
                    DeviceStatic = false;
                }
                else
                {
                    erro = ClassAi.AiInit(this.AiPath).ToString();
                    DeviceStatic = true;
                }

                if (!string.IsNullOrEmpty(erro))
                {
                    // 处理错误，例如显示错误消息
                    //MessageBox.Show(erro, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            //AddOutputParam("区域", "HRegion", OutRegion);
            //AddOutputParam("分类结果", "int", Type);
            //AddOutputParam("距离", "double", 55);
            try
            {


                AddOutputParam("钉帽圆心X", "double", OutSideCircleX);
                AddOutputParam("钉帽圆心Y", "double", OutSideCircleY);
                AddOutputParam("钉帽圆半径", "double", InSideCircleX);
                AddOutputParam("偏钉面积", "double", InSideCircleY);
                
            }
            catch (Exception)
            {
                //AddOutputParam("面积", "double", 0);
                //AddOutputParam("X坐标", "double", 0);
                //AddOutputParam("Y坐标", "double", 0);
            }
        }

        #region Prop
        [NonSerialized]
        int ChannelNum = 1;
        [NonSerialized]
        private HRegion _ReduceRegion;
        [NonSerialized]
        public HImage OutputImage;
        private bool _DeviceStatic =false;
        private string _AiPath ="";
        private double cONF_THRESHOLD = 0.5, nMS_THRESHOLD = 0.5;
        private int _DefectNum = 4;
        private eLocationClass _SelectLocation = eLocationClass.Center;
        private double _OutSideCircleX, _OutSideCircleY, _InSideCircleX, _InSideCircleY;
        public double OutSideCircleX
        {
            get { return _OutSideCircleX; }
            set { _OutSideCircleX = value; RaisePropertyChanged(); }
        }
        public double OutSideCircleY
        {
            get { return _OutSideCircleY; }
            set { _OutSideCircleY = value; RaisePropertyChanged(); }
        }
        public double InSideCircleX
        {
            get { return _InSideCircleX; }
            set { _InSideCircleX = value; RaisePropertyChanged(); }
        }
        public double InSideCircleY
        {
            get { return _InSideCircleY; }
            set { _InSideCircleY = value; RaisePropertyChanged(); }
        }

        public int DefectNum
        {
            get { return _DefectNum; }
            set { Set(ref _DefectNum, value); }
        }
        public bool DeviceStatic
        {
            get { return _DeviceStatic; }
            set { Set(ref _DeviceStatic, value); }
        }
        public double CONF_THRESHOLD
        {
            get{return cONF_THRESHOLD; }
            set { Set(ref cONF_THRESHOLD, value); }
        }
        public double NMS_THRESHOLD
        {
            get { return nMS_THRESHOLD; }
            set { Set(ref nMS_THRESHOLD, value); }
        }
        public Array LocationSource { get; set; } = Enum.GetValues(typeof(eLocationClass));
        public eLocationClass SelectLocation
        {
            get { return _SelectLocation; }
            set { Set(ref _SelectLocation, value); }
        }

        public string AiPath
        {
            get { return _AiPath; }
            set { Set(ref _AiPath, value); }
        }

        private bool _IsOpen, _IsDispAiRegion = false;
        public bool IsDispAiRegion
        {
            get { return _IsDispAiRegion; }
            set { _IsDispAiRegion = value; RaisePropertyChanged(); }
        }
        public bool IsOpen
        {
            get { return _IsOpen; }
            set { _IsOpen = value; RaisePropertyChanged(); }
        }
        //新增判断显示
        private bool _IsCross, _IsFindRegions, _IsFindCircles = false;

        public bool IsCross
        {
            get { return _IsCross; }
            set { _IsCross = value; RaisePropertyChanged(); }
        }
        public bool IsFindRegions
        {
            get { return _IsFindRegions; }
            set { _IsFindRegions = value; RaisePropertyChanged(); }
        }
        public bool IsFindCircles
        {
            get { return _IsFindCircles; }
            set { _IsFindCircles = value; RaisePropertyChanged(); }
        }


        public HRegion ReduceRegion
        {
            get
            {
                if (_ReduceRegion == null)
                {
                    _ReduceRegion = new HRegion();
                }
                return _ReduceRegion;
            }
            set { Set(ref _ReduceRegion, value); }
        }
        [NonSerialized]
        HRegion OutRegion = new HRegion();
        [NonSerialized]
        int Type = 0;
        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
    

        private bool _OutPutMaxArea = false;
        public bool OutPutMaxArea
        {
            get { return _OutPutMaxArea; }
            set { _OutPutMaxArea = value; ExeModule(); RaisePropertyChanged(); }

        }
        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        //初始化，
        private LinkVarModel _Scale = new LinkVarModel() { Value=1 };
        public LinkVarModel Scale
        {
            get { return _Scale; }
            set { _Scale = value; RaisePropertyChanged(); }
        }



        private int _DrawSize = 10;
        /// <summary>
        /// 涂抹尺寸
        /// </summary>
        public int DrawSize
        {
            get { return _DrawSize; }
            set { Set(ref _DrawSize, value, new Action(() => SetBurshRegion())); }
        }
        [NonSerialized]
        public HXLDCont contour_xld;
        [NonSerialized]
        HRegion finalRegion = new HRegion();
        [NonSerialized]
        HObject brushRegion = new HObject();
        #endregion
        #region//后处理参数
        private bool useScore = false,useID =false, useLimtHeight = false ,useLimtWidth =false;
        public bool UseLimtWidth
        {
            get { return useLimtWidth; }
            set { Set(ref useLimtWidth, value); }
        }
        public bool UseLimtHeight
        {
            get { return useLimtHeight; }
            set { Set(ref useLimtHeight, value); }
        }
        public bool UseID
        {
            get { return useID; }
            set { Set(ref useID, value); }
        }
        public bool UseScore
        {
            get { return useScore; }
            set { Set(ref useScore, value); }
        }
        private double score = 0;
        public double Score
        {
            get { return score; }
            set { Set(ref score, value); }
        }
        private int iD = 1;
        public int ID
        {
            get { return iD; }
            set { Set(ref iD, value); }
        }
        private double limtHeight = 9999,limtWidth=9999;
        public double LimtHeight
        {
            get { return limtHeight; }
            set { Set(ref limtHeight, value); }
        }
        public double LimtWidth
        {
            get { return limtWidth; }
            set { Set(ref limtWidth, value); }
        }
        
        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as SealingPinYuhanView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (InputImageLinkText == null || InputImageLinkText =="")
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                view.mWindowH.hControl.MouseUp += HControl_MouseUp;

                GetDispImage(InputImageLinkText);
                //ExeModule();
                //view.mWindowH.DispObj(DispImage);
                //ImageChanged();
                //ThresholdChanged();
                

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
                        var view = ModuleView as SealingPinYuhanView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        [NonSerialized]
        private AI ClassAi;
        [NonSerialized]
        private CommandBase _AiInit;
        public CommandBase AiInit
        {
            get
            {
                if (_AiInit == null)
                {
                    _AiInit = new CommandBase((obj) =>
                    {
                        string erro = "";
                        if (ClassAi == null)
                            ClassAi = new AI();
                        
                        if (ClassAi.IsRunning)
                        {
                            ClassAi.AiClose(ref erro);
                            DeviceStatic = false;
                        }
                        else
                        {
                            erro = ClassAi.AiInit(this.AiPath).ToString();
                            DeviceStatic = true;
                        }
                        if (!string.IsNullOrEmpty(erro))
                        {
                            // 处理错误，例如显示错误消息
                            //MessageBox.Show(erro, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                return _AiInit;
            }
        }

        [NonSerialized]
        private CommandBase _OpenAiFolder;
        public CommandBase OpenAiFolder
        {
            get
            {
                if (_OpenAiFolder == null)
                {
                    _OpenAiFolder = new CommandBase((obj) =>
                    {
                        Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                        openFileDialog.Filter = "(*.xml,*.engine,*.hymson)|*.xml;*.engine*;*.hymson|All files(*.*)|*.*";
                        if ((bool)openFileDialog.ShowDialog())
                        {
                            AiPath = openFileDialog.FileName;
                        }
                    });
                }
                return _OpenAiFolder;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    if (InputImageLinkText == null) return;
                    GetDispImage(InputImageLinkText);
                    //ImageChanged();
                    break;
                case "Scale":
                    Scale.Text = obj.LinkName;
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
                            case eLinkCommand.MathNum:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},MathNumLink");
                                break;
                            case eLinkCommand.CenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},CenterY");
                                break;
                            case eLinkCommand.Scale:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Scale");
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
        private CommandBase _ClearPaintCommand;
        public CommandBase ClearPaintCommand
        {
            get
            {
                if (_ClearPaintCommand == null)
                {
                    _ClearPaintCommand = new CommandBase((obj) =>
                    {
                        finalRegion.Dispose();
                        var view = ModuleView as SealingPinYuhanView;
                        view.mWindowH.HobjectToHimage(DispImage);
                        view.mWindowH.WindowH.DispHobject(contour_xld, "green");
                    });
                }
                return _ClearPaintCommand;
            }
        }
        #endregion

        #region Method
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as SealingPinYuhanView;
                if (view == null) return; ;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                view.mWindowH.DispObj(finalRegion);
                if (index.Length < 1) return;
                RoiList[index] = roi;
                //switch (SelectedROIType)
                //{
                //    case eDrawShape.矩形:
                //        Rectangle2Region = (ROIRectangle2)roi;
                //        Rectangle2Region.Length1 = Math.Round(Rectangle2Region.Length1, 2);
                //        Rectangle2Region.Length2 = Math.Round(Rectangle2Region.Length2, 2);
                //        Rectangle2Region.MidC = Math.Round(Rectangle2Region.MidC, 2);
                //        Rectangle2Region.MidR = Math.Round(Rectangle2Region.MidR, 2);
                //        ReduceRegion.GenRectangle2(Rectangle2Region.MidR, Rectangle2Region.MidC, -Rectangle2Region.Phi, Rectangle2Region.Length1, Rectangle2Region.Length2);
                //        break;
                //    case eDrawShape.圆形:
                //        CircleRegion = (ROICircle)roi;
                //        CircleRegion.CenterX = Math.Round(CircleRegion.CenterX, 2);
                //        CircleRegion.CenterY = Math.Round(CircleRegion.CenterY, 2);
                //        CircleRegion.Radius = Math.Round(CircleRegion.Radius, 2);
                //        ReduceRegion.GenCircle(CircleRegion.CenterY, CircleRegion.CenterX, CircleRegion.Radius);
                //        break;
                //    default:
                //        break;
                //}
                //ThresholdChanged();
            }
            catch (Exception ex)
            {
            }
        }
        [NonSerialized]
        VMHWindowControl mWindowH;
        private void ShowImage(HImage image)
        {
            var view = ModuleView as SealingPinYuhanView;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
            }
            mWindowH.ClearWindow();
            mWindowH.HobjectToHimage(image);
        }
        private void ShowRoi()
        {
            //var view = ModuleView as BlobView;
            bool dispDrawRoi = true;
            //if (view == null || view.IsClosed)
            //{
            //    dispDrawRoi = false;
            //}

            var view = ModuleView as SealingPinYuhanView;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispDrawRoi = false;
            }
            else
            {
                mWindowH = view.mWindowH;
            }


            //if (dispDrawRoi)
            //{
            //    switch (SelectedROIType)
            //    {
            //        case eDrawShape.矩形:
            //            mWindowH.WindowH.DispROI(ModuleParam.ModuleEncode + ModuleParam.ModuleName + ROIType.Rectangle2, Rectangle2Region);
            //            break;

            //        case eDrawShape.圆形:
            //            mWindowH.WindowH.DispROI(ModuleParam.ModuleEncode + ModuleParam.ModuleName + ROIType.Circle, CircleRegion);
            //            break;
            //        default:
            //            break;
            //    }
            //}

            if (ReduceRegion != null && ReduceRegion.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(ReduceRegion)));
            }
            if (OutRegion != null && OutRegion.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", new HObject(OutRegion),true));
            }
            List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            foreach (HRoi roi in roiList)
            {
                if (roi.roiType == HRoiType.文字显示)
                {
                    HText roiText = (HText)roi;
                    ShowTool.SetFont(mWindowH.hControl.HalconWindow, roiText.size, "false", "false");
                    ShowTool.SetMsg(mWindowH.hControl.HalconWindow, roiText.text, "image", roiText.row, roiText.col, roiText.drawColor, "false");
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                }
            }

        }
        public void ShowImageAndRoi(HImage image)
        {
            ShowImage(image);
            ShowRoi();
        }
        private void SetBurshRegion()
        {
            HObject ho_temp_brush = new HObject();
            HTuple hv_Row1 = 10, hv_Column1 = 10, hv_Row2 = null, hv_Column2 = null;
            HTuple imageWidth, imageHeight;
            HImage image = new HImage(DispImage);
            image.GetImageSize(out imageWidth, out imageHeight);
            //switch (DrawShape)
            //{
            //    case eDrawShape.圆形:
            //        HOperatorSet.GenCircle(out ho_temp_brush, imageWidth / 2, imageHeight / 2, DrawSize);
            //        if (hv_Row1.D != 0)
            //        {
            //            brushRegion.Dispose();
            //            brushRegion = ho_temp_brush;
            //        }
            //        break;
            //    case eDrawShape.矩形:
            //        HOperatorSet.GenRectangle1(out ho_temp_brush, 0, 0, DrawSize, DrawSize);
            //        if (hv_Row1.D != 0)
            //        {
            //            brushRegion.Dispose();
            //            brushRegion = ho_temp_brush;
            //        }
            //        break;
            //    default:
            //        break;
            //}

        }




        #endregion
        
    }
}
