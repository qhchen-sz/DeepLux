using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.Envelope.Views;
using Plugin.GrabImage.Model;
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
using HV.Services;

namespace Plugin.Envelope.ViewModels
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

    [Category("包膜机")]
    [DisplayName("包膜机算法")]
    [ModuleImageName("Envelope")]
    [Serializable]
    public class EnvelopeViewModel : ModuleBase
    {
        [NonSerialized]
        HImage _image;
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
                ResultValue1 = 0;
                ResultValue2 = 0;
                ResultValue3 = 0;
                ResultValue4= 0;
                ResultValue5 = 0;
                ResultValue6 = 0;
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
                EnvelopeViewModel aiViewModel = this;
                HImage Result;
                if (DispImage !=null && DispImage.IsInitialized())
                {

                
                var task = Task.Run(() =>
                {
                    HImage Temp = DispImage.CopyImage();
                    Logger.AddLog(Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName + ":"+this.ModuleParam.ModuleName+ "进入推理：", eMsgType.Warn);
                    Result = ClassAi.AiRun(Temp, ref erro, this.CONF_THRESHOLD, this.NMS_THRESHOLD);
                    Logger.AddLog(Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName + ":" + this.ModuleParam.ModuleName + "推理结束：", eMsgType.Warn);
                    //面积
                    //像素当量换算

                    double.TryParse(GetLinkValue(Scale).ToString(), out double SS);
                    ResultValue6 = Result.Threshold(3.0, 3.0).RegionFeatures("area") * SS * SS;
                    ResultValue4= Result.Threshold(3.0, 3.0).RegionFeatures("width") * SS ;
                    ResultValue5 = Result.Threshold(3.0, 3.0).RegionFeatures("height") * SS;
                    _image = new HImage();
                    _image = Result.ScaleImage(50.0, 0);
                    if (IsDispAiRegion)
                    {
                        HRegion region1 = new HRegion();
                        HRegion region2 = new HRegion();
                        HRegion region3_1 = new HRegion();
                        HRegion region4 = new HRegion();
                        region3_1 = Result.Threshold(3.0, 3.0);
                        region1 = Result.Threshold(1.0, 1.0);
                        region2 = Result.Threshold(2.0, 2.0);
                        region4 = Result.Threshold(4.0, 4.0);
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(region1), true));
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai2", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(region2), true));
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai3", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)4).ToString(), new HObject(region3_1), true));
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai4", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)5).ToString(), new HObject(region4), true));
                        region3_1.Dispose();
                        region1.Dispose();
                        region2.Dispose();
                        region4.Dispose();
                    }

                    Logger.AddLog(Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName + ":" + this.ModuleParam.ModuleName + "进入传统寻边算法：", eMsgType.Warn);
                    Algorithm.Find_RongDian(Result.ScaleImage(60.0,0), out HObject Line, out HObject Arrow, out HObject Cross, SelectLocation.ToString(), out HTuple Distance1, out HTuple Distance2);
                    //HOperatorSet.WriteImage(Result.ScaleImage(60.0, 0), "bmp", 0, @"C:\Users\Administrator\Desktop\ai\rongdian\1.bmp");
                    Result.Dispose();
                    Logger.AddLog(Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName + ":" + this.ModuleParam.ModuleName + "传统寻边算法结束：", eMsgType.Warn);
                    ResultValue1 = Math.Round((double)(Distance1[0] * SS), 2);
                    ResultValue2 = 0;
                    ResultValue3 = 0;
                    //ResultValue4 = 0;
                    //ResultValue5 = 0;
                    if (SelectLocation.ToString() == "Left")
                    {
                        ResultValue2 = Math.Round((double)(Distance2[0] * SS), 2);
                    }
                    else if (SelectLocation.ToString() == "Right")
                    {
                        ResultValue3 = Math.Round((double)(Distance2[0] * SS), 2);
                    }
                    else
                    {
                        ResultValue2 = Math.Round((double)(Distance2[0] * SS), 2);
                        ResultValue3 = Math.Round((double)(Distance2[1] * SS), 2);
                        

                    }
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 1, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.red.ToString(), new HObject(Line)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 2, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(Arrow)));
                    Line.Dispose();
                    Arrow.Dispose();
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 3, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(Line3)));
                    //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 4, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.red.ToString(), new HObject(DispLine)));

                    //新增判断显示
                    //if (IsHeatFusionJoint)
                    //{
                    //    HRegion region1 = new HRegion();
                    //    region1 = Result.Threshold(1.0, 1.0);
                    //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(region1), false));
                    //}
                    //if (IsTopCover)
                    //{
                    //    HRegion region2 = new HRegion();
                    //    region2 = Result.Threshold(2.0, 3.0);
                    //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai2", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(region2), false));
                    //}

                    HRegion region3 = new HRegion();
                    region3 = Result.Threshold(3.0, 3.0);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai3_1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)4).ToString(), new HObject(region3), false));
                    region3.Dispose();
                    //if (IsDiaphragm)
                    //{
                    //    HRegion region4 = new HRegion();
                    //    region4 = Result.Threshold(4.0, 4.0);
                    //    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai4", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)5).ToString(), new HObject(region4), false));
                    //}
                    ShowHRoi();
                    ChangeModuleRunStatus(eRunStatus.OK);
                }
                

                );
                if (task.Wait(TimeSpan.FromSeconds(2))) // 30秒超时
                {
                     
                }
                else
                {
                    // 处理超时
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.AddLog("AI推理超时",eMsgType.Error);
                    return false;
                }
                }
                else
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                //HImage Result = ClassAi.AiRun(temp, ref erro, this.CONF_THRESHOLD, this.NMS_THRESHOLD);



                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                OutRegion.Dispose();

                return false;
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

            //像素当量换算
            //double.TryParse(Scale.Text, out double scale);
            try
            {

                AddOutputParam("熔点面积", "double", (ResultValue6));
                AddOutputParam("熔点到隔膜距离", "double", (ResultValue1 ));
                AddOutputParam("左膜到顶盖距离", "double", (ResultValue2 ));
                AddOutputParam("右膜到顶盖距离", "double", (ResultValue3 ));
                AddOutputParam("熔点宽度", "double",(ResultValue4 ));
                AddOutputParam("熔点高度", "double", (ResultValue5 ));
                AddOutputParam("ai图像", "HImage", _image);
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
        //[NonSerialized]
        //public HImage OutputImage;
        private bool _DeviceStatic =false;
        private string _AiPath ="";
        private double cONF_THRESHOLD = 0.5, nMS_THRESHOLD = 0.5;
        private int _DefectNum = 4;
        private eLocationClass _SelectLocation = eLocationClass.Center;
        private double _ResultValue1, _ResultValue2, _ResultValue3, _ResultValue4, _ResultValue5,_ResultValue6;
        public double ResultValue1
        {
            get { return _ResultValue1; }
            set { _ResultValue1 = value; RaisePropertyChanged(); }
        }
        public double ResultValue2
        {
            get { return _ResultValue2; }
            set { _ResultValue2 = value; RaisePropertyChanged(); }
        }
        public double ResultValue3
        {
            get { return _ResultValue3; }
            set { _ResultValue3 = value; RaisePropertyChanged(); }
        }
        public double ResultValue4
        {
            get { return _ResultValue4; }
            set { _ResultValue4 = value; RaisePropertyChanged(); }
        }
        public double ResultValue5
        {
            get { return _ResultValue5; }
            set { _ResultValue5 = value; RaisePropertyChanged(); }
        }
        public double ResultValue6
        {
            get { return _ResultValue6; }
            set { _ResultValue6 = value; RaisePropertyChanged(); }
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

        private bool _IsOpen, _IsDispAiRegion=false;
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
        private bool _IsHeatFusionJoint, _IsTopCover, _IsBlueMembrane, _IsDiaphragm = false;

        public bool IsHeatFusionJoint
        {
            get { return _IsHeatFusionJoint; }
            set { _IsHeatFusionJoint = value; RaisePropertyChanged(); }
        }
        public bool IsTopCover
        {
            get { return _IsTopCover; }
            set { _IsTopCover = value; RaisePropertyChanged(); }
        }
        public bool IsBlueMembrane
        {
            get { return _IsBlueMembrane; }
            set { _IsBlueMembrane = value; RaisePropertyChanged(); }
        }
        public bool IsDiaphragm
        {
            get { return _IsDiaphragm; }
            set { _IsDiaphragm = value; RaisePropertyChanged(); }
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

        //像素当量，初始化，
        private LinkVarModel _Scale = new LinkVarModel() { Value = 1 };
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
            var view = ModuleView as EnvelopeView;
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
                        var view = ModuleView as EnvelopeView;
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
                        var view = ModuleView as EnvelopeView;
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
                var view = ModuleView as EnvelopeView;
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
            var view = ModuleView as EnvelopeView;
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

            var view = ModuleView as EnvelopeView;
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
