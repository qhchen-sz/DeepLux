using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.SealingPinHanHou.Views;
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

namespace Plugin.SealingPinHanHou.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum,
        CenterY,
        Scale,
        DefectType
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
    [DisplayName("密封钉焊后算法")]
    [ModuleImageName("SealingPinHanHou")]
    [Serializable]
    public class SealingPinHanHouViewModel : ModuleBase
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
            // 1. 在try外声明需要释放的资源
            HImage Result = null;
            // 假设ResultList也需要某种形式的清理
            EVDll.AI_GpuALL.AI_GpuALL.ResultList ResultList = null;
            HRegion region1 = null, region2 = null,region3 =null;
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
                if (ClassAi == null || !ClassAi.IsRunning || DispImage==null )
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                SealingPinHanHouViewModel aiViewModel = this;
                var task1 = Task.Run(() =>
                {
                    string localErro1 = "";
                    var result = ClassAi.AiRun(DispImage, ref localErro1, this.CONF_THRESHOLD, this.NMS_THRESHOLD);
                    if (!string.IsNullOrEmpty(localErro1)) erro += $"AiRun Error: {localErro1}; ";
                    return result;
                });

                var task2 = Task.Run(() =>
                {
                    string localErro2 = "";
                    var result = ClassAi.AiRunDetection(DispImage, ref localErro2, this.CONF_THRESHOLD, this.NMS_THRESHOLD);
                    if (!string.IsNullOrEmpty(localErro2)) erro += $"AiRunDetection Error: {localErro2}; ";
                    return result;
                });

                // 等待两个任务完成
                Task.WaitAll(task1, task2);
                // 并行执行两个AI推理方法
                Result = task1.Result;
                ResultList = task2.Result;
                //string localErro1 = "" , localErro2 = "";
                //Result =  ClassAi.AiRun(DispImage, ref localErro1, this.CONF_THRESHOLD, this.NMS_THRESHOLD);
                //ResultList = ClassAi.AiRunDetection(DispImage, ref localErro2, this.CONF_THRESHOLD, this.NMS_THRESHOLD);
                region2 = new HRegion();
                region3 = new HRegion();
                region2 = Result.Threshold(3.0, 3.0); 
                region3 = Result.Threshold(4.0, 4.0);
                region1 = new HRegion();
                region1 = Result.Threshold(2.0, 2.0).Connection().SelectShapeStd("max_area", 70);
                if (IsDispAiRegion)
                {

                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(region1), true));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai2", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(region2), true));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai3", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)4).ToString(), new HObject(region3), true));
                    

                }
                double area1 = region2.Connection().SelectShapeStd("max_area",70.0).FillUp().AreaCenter(out double Row1,out double Column1);
                double area2 = region3.Connection().Intersection(region2.Connection().SelectShapeStd("max_area", 70.0).FillUp()).SelectShapeStd("max_area", 70.0).FillUp().AreaCenter(out double Row2, out double Column2);
                double area3 = region1.Connection().SelectShapeStd("max_area", 70.0).AreaCenter(out double Row3, out double Column3);
                region2.Dispose();
                region3.Dispose();
                region1.Dispose();

                HOperatorSet.DistancePp(Row1, Column1, Row2, Column2, out HTuple distance);
                HOperatorSet.TupleConcat(Row1, Row2, out HTuple Row);
                HOperatorSet.TupleConcat(Column1, Column2, out HTuple Column);
                HOperatorSet.GenCrossContourXld(out HObject Cross, Row, Column,26,0.785398);
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai4", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)0).ToString(), new HObject(Cross)));
                Cross.Dispose();
                DefectArea1 = 0;
                DefectArea2 = 0;
                DefectArea3 = 0;
                DefectArea4 = 0;
                DefectArea5 = -9999;
                DefectArea6 = 0;
                double[] resD = DefectSelect(Result, ref ResultList);

                double.TryParse(GetLinkValue(Scale).ToString(), out double SS);
                DefectArea1 = Math.Round(resD[0] * SS * SS, 2);
                DefectArea2 = Math.Round(resD[1] * SS * SS, 2);
                DefectArea3 = Math.Round(resD[2] * SS * SS, 2);
                DefectArea4 = Math.Round(resD[3] * SS * SS, 2);
                if (area1 != 0 && area2 != 0)
                {
                    DefectArea5 = Math.Round((double)distance * SS, 2);
                }
                DefectArea6 = Math.Round(area3 * SS * SS, 2);
                ChangeModuleRunStatus(eRunStatus.OK);
                ShowHRoi();
                Result.Dispose();
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
                region1?.Dispose();
                region2?.Dispose();
                Result?.Dispose();
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
                    erro = ClassAi.AiInit(this.AiPath1,this.AiPath2).ToString();
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


                AddOutputParam("最大焊渣面积", "double", DefectArea1);
                AddOutputParam("最大爆点面积", "double", DefectArea2);
                AddOutputParam("最大针孔面积", "double", DefectArea3);
                AddOutputParam("最大焊黑面积", "double", DefectArea4);
                AddOutputParam("偏焊圆心距", "double", DefectArea5);
                AddOutputParam("焊道面积", "double", DefectArea6);

            }
            catch (Exception)
            {
                //AddOutputParam("面积", "double", 0);
                //AddOutputParam("X坐标", "double", 0);
                //AddOutputParam("Y坐标", "double", 0);
            }
        }

        public double[] DefectSelect(HImage hImage, ref EVDll.AI_GpuALL.AI_GpuALL.ResultList resultList)
        {
            double[] doubles = new double[4] {0,0,0,0};
            HRegion DefectRegion1 = new HRegion(); DefectRegion1.GenEmptyObj();
            HRegion DefectRegion2 = new HRegion(); DefectRegion2.GenEmptyObj();
            HRegion DefectRegion3 = new HRegion(); DefectRegion3.GenEmptyObj();
            HRegion DefectRegion4 = new HRegion(); DefectRegion4.GenEmptyObj();
            HRegion Region = hImage.Threshold(2.0, 2.0).Connection().SelectShapeStd("max_area", 70);//焊道
            HRegion RegionQinXi = hImage.Threshold(1.0, 1.0).FillUp();//清洗
            hImage.Dispose();
            HRegion hRegion = new HRegion();
            double area = 0;
            double areatemp = 0;
            string[] type = new string[4] { "焊渣","爆点","针孔","焊黑" };
            string dispstr = "";
            for (int i = 0; i < resultList.X.Count; i++)
            {
                hRegion.GenRectangle2(resultList.Y[i], resultList.X[i], 0, resultList.Length1[i], resultList.Length2[i]);
                area = hRegion.Intersection(Region).RegionFeatures("area");
                areatemp = hRegion.Intersection(RegionQinXi).RegionFeatures("area");
                switch (resultList.Id[i])
                {
                    case 1://焊渣
                        if(area > 0)
                        {
                            resultList.Id[i] = 2;
                            DefectRegion2 =DefectRegion2.ConcatObj(new HRegion(hRegion));
                            dispstr = type[1] + ":" + resultList.Score[i].ToString("f2");
                            if (resultList.Length1[i] * resultList.Length2[i] * 4 > doubles[1])
                                doubles[1] = resultList.Length1[i] * resultList.Length2[i] * 4;
                        }
                        else
                        {
                            if (areatemp > 0)
                            {
                                DefectRegion1 = DefectRegion1.ConcatObj(new HRegion(hRegion));
                                dispstr = type[0] + ":" + resultList.Score[i].ToString("f2");
                                if (resultList.Length1[i] * resultList.Length2[i] * 4 > doubles[0])
                                    doubles[0] = resultList.Length1[i] * resultList.Length2[i] * 4;
                            }
                            else
                                continue;
                        }

                        break;
                    case 2://爆点
                        if (area > 0)
                        {
                            DefectRegion2 = DefectRegion2.ConcatObj(new HRegion(hRegion));
                            dispstr = type[1] + ":" + resultList.Score[i].ToString("f2");
                            if (resultList.Length1[i] * resultList.Length2[i] * 4 > doubles[1])
                                doubles[1] = resultList.Length1[i] * resultList.Length2[i] * 4;
                        }
                        else
                        {
                            if (areatemp > 0)
                            {
                                DefectRegion1 = DefectRegion1.ConcatObj(new HRegion(hRegion));
                                dispstr = type[0] + ":" + resultList.Score[i].ToString("f2");
                                resultList.Id[i] = 1;
                                if (resultList.Length1[i] * resultList.Length2[i] * 4 > doubles[0])
                                    doubles[0] = resultList.Length1[i] * resultList.Length2[i] * 4;
                            }
                        }
                        break;
                    case 3://针孔
                        if (area > 0)
                        {
                            DefectRegion3 = DefectRegion3.ConcatObj(new HRegion(hRegion));
                            dispstr = type[2] + ":" + resultList.Score[i].ToString("f2");
                            if (resultList.Length1[i] * resultList.Length2[i] * 4 > doubles[2])
                                doubles[2] = resultList.Length1[i] * resultList.Length2[i] * 4;
                        }
                        else
                        {
                            resultList.Id[i] = 0;
                            continue;
                        }
                        break;
                    case 4://焊黑

                        if (area > resultList.Length1[i] * resultList.Length2[i] * 2)
                        {
                            DefectRegion4 =DefectRegion4.ConcatObj(new HRegion(hRegion));
                            dispstr = type[3] + ":" + resultList.Score[i].ToString("f2");
                            if (resultList.Length1[i] * resultList.Length2[i] * 4 > doubles[3])
                                doubles[3] = resultList.Length1[i] * resultList.Length2[i] * 4;
                        }
                        else
                        {
                            resultList.Id[i] = 0;
                            continue;
                        }
                        break;
                    default:
                        break;
                }

                ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName + i, ModuleParam.Remarks, HRoiType.文字显示, ((eAiColor)(resultList.Id[i])).ToString(), dispstr, resultList.X[i]- resultList.Length1[i], resultList.Y[i]+ resultList.Length2[i], 56));
            }
            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 1, ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)1).ToString(), new HObject(DefectRegion1)));
            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 2, ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(DefectRegion2)));
            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 3, ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(DefectRegion3)));
            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 4, ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)4).ToString(), new HObject(DefectRegion4)));
            //ShowHRoi();
            DefectRegion1.Dispose();
            DefectRegion2.Dispose();
            DefectRegion3.Dispose();
            DefectRegion4.Dispose();
            Region.Dispose();
            hRegion.Dispose();
            RegionQinXi.Dispose();
            return doubles;

        }

        #region Prop
        [NonSerialized]
        int ChannelNum = 1;
        [NonSerialized]
        private HRegion _ReduceRegion;
        [NonSerialized]
        public HImage OutputImage;
        private bool _DeviceStatic =false;
        private string _AiPath1 ="" , _AiPath2 = "";
        private double cONF_THRESHOLD = 0.5, nMS_THRESHOLD = 0.5;
        private int _DefectNum = 4;
        private eLocationClass _SelectLocation = eLocationClass.Center;
        private double _DefectArea1, _DefectArea2, _DefectArea3, _DefectArea4, _DefectArea5, _DefectArea6;
        private string _DefectType = "焊渣,爆点,针孔,焊黑";
        public string DefectType
        {
            get { return _DefectType; }
            set { _DefectType = value; RaisePropertyChanged(); }
        }
        public double DefectArea1
        {
            get { return _DefectArea1; }
            set { _DefectArea1 = value; RaisePropertyChanged(); }
        }
        public double DefectArea2
        {
            get { return _DefectArea2; }
            set { _DefectArea2 = value; RaisePropertyChanged(); }
        }
        public double DefectArea3
        {
            get { return _DefectArea3; }
            set { _DefectArea3 = value; RaisePropertyChanged(); }
        }
        public double DefectArea4
        {
            get { return _DefectArea4; }
            set { _DefectArea4 = value; RaisePropertyChanged(); }
        }
        public double DefectArea5
        {
            get { return _DefectArea5; }
            set { _DefectArea5 = value; RaisePropertyChanged(); }
        }
        public double DefectArea6
        {
            get { return _DefectArea6; }
            set { _DefectArea6 = value; RaisePropertyChanged(); }
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

        public string AiPath1
        {
            get { return _AiPath1; }
            set { Set(ref _AiPath1, value); }
        }
        public string AiPath2
        {
            get { return _AiPath2; }
            set { Set(ref _AiPath2, value); }
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
            var view = ModuleView as SealingPinHanHouView;
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
                        var view = ModuleView as SealingPinHanHouView;
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
                            erro = ClassAi.AiInit(this.AiPath1,this.AiPath2).ToString();
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
                            var ee = obj;
                            if(obj.ToString() == "定位")
                            {
                                AiPath1 = openFileDialog.FileName;
                            }
                            else if(obj.ToString() == "缺陷")
                            {
                                AiPath2 = openFileDialog.FileName;
                            }

                            
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
                        var view = ModuleView as SealingPinHanHouView;
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
                var view = ModuleView as SealingPinHanHouView;
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
            var view = ModuleView as SealingPinHanHouView;
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

            var view = ModuleView as SealingPinHanHouView;
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
