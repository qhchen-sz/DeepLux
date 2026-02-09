using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.ThreeDimsAI.Views;
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
/*using EVDll;*/
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using Application = System.Windows.Application;
using System.Runtime.InteropServices;

namespace Plugin.ThreeDimsAI.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum,
        CenterY,
    }
    public enum eOperateCommand
    {
        StartLearn,
        Edit,
        EndLearn,
        Cancel
    }
    public enum eAiClass
    {
        CPU,
        GPU,
        无,
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
    [DisplayName("密封钉AI")]
    [ModuleImageName("ThreeDimsAI")]
    [Serializable]
    public class ThreeDimsAiViewModel : ModuleBase
    {
        //UI友好型结果显示
        public class DefectResult
        {
            public string LabelName { get; set; }
            public float Score { get; set; }
            public float Height { get; set; }
            public float Area { get; set; }

            // —— ROI 相关（不显示在 UI）——
            public double CX { get; set; }
            public double CY { get; set; }
            public double HalfWidth { get; set; }
            public double HalfHeight { get; set; }
            public double Angle { get; set; }
        }

/*        [NonSerialized]
        public ObservableCollection<DefectResult> UIDefects
                    = new ObservableCollection<DefectResult>();*/


        public eLinkCommand InputImageLinkCmd
        {
            get { return eLinkCommand.InputImageLink; }
        }

        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
            {
                return;
            }
            InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        //TODO
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
                ThreeDimsAiViewModel aiViewModel = this;
                if (AiClass == eAiClass.GPU)
                {
                    //打开AI模型才执行
                    if (Enable)
                    {
                        if (OutRegion == null)
                        {
                            OutRegion = new HRegion();
                        }
                        OutRegion.GenEmptyObj();
                        SMFD.Defect defects;
                        SMFD.ImgPara img_para = default;
                        SMFD.HalconToImgPara(DispImage, out img_para);
                        
                        bool exe_flag = SMFD.RunSMFD(ref img_para, (float)this.CONF_THRESHOLD, (float)this.IOU_THRESHOLD, out defects);
                        if (!exe_flag) {
                            Logger.AddLog("AI模型执行失败！", eMsgType.Error);
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }
                        //手动释放内存
                        Marshal.FreeHGlobal(img_para.data);
                        img_para.data = IntPtr.Zero;
                        // 预分配容量，避免多次扩容
                        List<double> CX = new List<double>(defects.size);
                        List<double> CY = new List<double>(defects.size);
                        List<double> HalfWidth = new List<double>(defects.size);
                        List<double> HalfHeight = new List<double>(defects.size);
                        List<double> Angle = new List<double>(defects.size);
                        List<int> Label = new List<int>(defects.size);
                        List<float> Score = new List<float>(defects.size);
                        List<float> Height = new List<float>(defects.size);
                        List<float> AreaBBox = new List<float>(defects.size);
                        string[] CLASS_NAMES = { "pinhole", "crap", "scatter" };
                        List<string> LabelNames = new List<string>(defects.size);
                        for (int i = 0; i < defects.size; i++)
                        {
                            CX.Add(defects.cx[i]);
                            CY.Add(defects.cy[i]);
                            HalfWidth.Add(defects.half_width[i]);
                            HalfHeight.Add(defects.half_height[i]);
                            Angle.Add(defects.angle[i]);
                            Label.Add(defects.label_id[i]);
                            LabelNames.Add(CLASS_NAMES[defects.label_id[i]]);
                            Score.Add(defects.score[i]);
                            Height.Add(defects.height[i]);
                            AreaBBox.Add(defects.area_bbox[i]);
                        }

                        // 1️、后台线程：构建快照
                        var results = new List<DefectResult>(defects.size);
                        for (int i = 0; i < defects.size; i++)
                        {
                            results.Add(new DefectResult
                            {
                                LabelName = CLASS_NAMES[defects.label_id[i]],
                                Score = defects.score[i],
                                Height = defects.height[i],
                                Area = defects.area_bbox[i],
                                CX = defects.cx[i],
                                CY = defects.cy[i],
                                HalfWidth = defects.half_width[i],
                                HalfHeight = defects.half_height[i],
                                Angle = defects.angle[i]
                            });
                        }

                        // 2️、UI 线程：只消费快照
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (UIDefects == null)
                            {
                                UIDefects = new ObservableCollection<DefectResult>();
                            }
                            UIDefects.Clear();
                            foreach (var r in results)
                                UIDefects.Add(r);
                        }));

                        /*                        Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    if (UIDefects == null)
                                                    {
                                                        UIDefects = new ObservableCollection<DefectResult>();
                                                    }
                                                    UIDefects.Clear();

                                                    for (int i = 0; i < defects.size; i++)
                                                    {
                                                        UIDefects.Add(new DefectResult
                                                        {
                                                            LabelName = CLASS_NAMES[defects.label_id[i]],
                                                            Score = defects.score[i],
                                                            Height = defects.height[i],
                                                            Area = defects.area_bbox[i]
                                                        });
                                                    }
                                                });*/

                        HObject roi = new HObject();
                        /*                Type = listResult.ResultList.Id[0];*/
                        roi.GenEmptyObj();
                        for (int i = 0; i < Label.Count; i++)
                        {
                            HOperatorSet.GenRectangle2(out HObject rectangle, CY[i], CX[i], Angle[i], HalfWidth[i], HalfHeight[i]);
                            HOperatorSet.ConcatObj(roi, rectangle, out roi);
                            rectangle.Dispose();
                            /*                    string dispstr = "类别:" +LabelNames[i] + "得分:" + Score[i].ToString("f2");*/
                            string dispstr = "类别:" + LabelNames[i] + "  得分:" + Score[i].ToString("f2");

                            ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName + i, ModuleParam.Remarks, HRoiType.文字显示, "red", dispstr, CX[i], CY[i], 16));

                        }
                        /*                HOperatorSet.CountObj(roi, out HTuple num);*/

                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "red", new HObject(roi)));
                        ShowHRoi();
                        roi.Dispose();
                        ChangeModuleRunStatus(eRunStatus.OK);
                    }
                    else {
                        Logger.AddLog("AI模型未打开！", eMsgType.Error);
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                }
                else {
                    Logger.AddLog("不支持的AI类型！", eMsgType.Error);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                
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

        //        public override void AddOutputParams()
        //        {
        //            base.AddOutputParams();
        //            AddOutputParam("区域", "HRegion", OutRegion);
        ///*            AddOutputParam("分类结果", "int", Type);*/
        //            //AddOutputParam("距离", "double", 55);
        //            try
        //            {
        //                if (OutRegion==null)
        //                {
        //                    OutRegion = new HRegion();
        //                }

        //                //AddOutputParam("面积", "double", OutRegion.Area.D);
        //                //AddOutputParam("X坐标", "double", OutRegion.Column.D);
        //                //AddOutputParam("Y坐标", "double", OutRegion.Row.D);
        //            }
        //            catch (Exception)
        //            {
        //                //AddOutputParam("面积", "double", 0);
        //                //AddOutputParam("X坐标", "double", 0);
        //                //AddOutputParam("Y坐标", "double", 0);
        //            }
        //        }

        #region Prop
        //[NonSerialized]
        //int ChannelNum = 1;
        [NonSerialized]
        private HRegion _ReduceRegion;
        [NonSerialized]
        public HImage OutputImage;
        //private bool _DeviceStatic =false;
        private string _AiPath ="";
        private double cONF_THRESHOLD = 0.4, iOU_THRESHOLD = 0.65;
        private eAiClass _AiClass= eAiClass.GPU;
        //private int _DefectNum = 1;
        //public int DefectNum
        //{
        //    get { return _DefectNum; }
        //    set { Set(ref _DefectNum, value); }
        //}
        //public bool DeviceStatic
        //{
        //    get { return _DeviceStatic; }
        //    set { Set(ref _DeviceStatic, value); }
        //}
        public double CONF_THRESHOLD
        {
            get{return cONF_THRESHOLD; }
            set { Set(ref cONF_THRESHOLD, value); }
        }
        public double IOU_THRESHOLD
        {
            get { return iOU_THRESHOLD; }
            set { Set(ref iOU_THRESHOLD, value); }
        }
        /*        public Array AiClassSource { get; set; } = Enum.GetValues(typeof(eAiClass));*/
        public Array AiClassSource =>
            Enum.GetValues(typeof(eAiClass))
                .Cast<eAiClass>()
                .Where(v => Enum.IsDefined(typeof(eAiClass), v))
                .ToArray();

        public eAiClass AiClass
        {
            get { return _AiClass; }
            set
            {
                //    if (!Enum.IsDefined(typeof(eAiClass), value))
                //    {
                //        _AiClass = eAiClass.GPU; // 或 eAiClass.无
                //    }
                //    else
                //    {
                //        _AiClass = value;
                //    }
                //    RaisePropertyChanged();
                //}
                Set(ref _AiClass, value);
            }
        }
        public string AiPath
        {
            get { return _AiPath; }
            set { Set(ref _AiPath, value); }
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
/*        [NonSerialized]
        int Type = 0;*/
        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
    

        //private bool _OutPutMaxArea = false;
        //public bool OutPutMaxArea
        //{
        //    get { return _OutPutMaxArea; }
        //    set { _OutPutMaxArea = value; ExeModule(); RaisePropertyChanged(); }

        //}
        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
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
            var view = ModuleView as AiView;
            view = ModuleView as AiView;
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
                GetDispImage(InputImageLinkText);
                //view.mWindowH.DispObj(DispImage);
                //ImageChanged();
                //ThresholdChanged();
                view.mWindowH.hControl.MouseUp += HControl_MouseUp;
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
                        var view = ModuleView as AiView;
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
                        var view = ModuleView as AiView;
                        view.mWindowH.HobjectToHimage(DispImage);
                        view.mWindowH.WindowH.DispHobject(contour_xld, "green");
                    });
                }
                return _ClearPaintCommand;
            }
        }

        [NonSerialized]
        private bool _Enable = false;
        public bool Enable
        {
            get { return _Enable; }
            set { Set(ref _Enable, value); }
        }
        [NonSerialized]
        private ObservableCollection<DefectResult> _uiDefects;

        public ObservableCollection<DefectResult> UIDefects
        {
            get { return _uiDefects; }
            set { Set(ref _uiDefects, value); }
        }

        [NonSerialized]
        private DefectResult _selectedDefect;
        public DefectResult SelectedDefect
        {
            get => _selectedDefect;
            set
            {
                if (ReferenceEquals(_selectedDefect, value))
                    return;
                Set(ref _selectedDefect, value);
                HighlightSelectedDefect();
            }
        }

        private DefectResult _lastHighlighted;
        private void HighlightSelectedDefect()
        {
            if (SelectedDefect == null)
                return;

            if (ReferenceEquals(_lastHighlighted, SelectedDefect))
                return;

            _lastHighlighted = SelectedDefect;

            try
            {
                // 清除之前的高亮 ROI（只清高亮，不清检测结果）
                ClearHighlightRoi();

                // 生成高亮矩形
                HOperatorSet.GenRectangle2(
                    out HObject rect,
                    SelectedDefect.CY,
                    SelectedDefect.CX,
                    SelectedDefect.Angle,
                    SelectedDefect.HalfWidth,
                    SelectedDefect.HalfHeight
                );

                // 显示高亮 ROI（用不同颜色）
                ShowHRoi(new HRoi(
                    ModuleParam.ModuleEncode,
                    "SelectedDefect",
                    ModuleParam.Remarks,
                    HRoiType.检测结果,
                    "yellow",
                    rect
                ));

/*                // 可选：显示文本
                string text = $"类别:{SelectedDefect.LabelName}  得分:{SelectedDefect.Score:F2}";
                ShowHRoi(new HText(
                    ModuleParam.ModuleEncode,
                    "SelectedText",
                    ModuleParam.Remarks,
                    HRoiType.文字显示,
                    "yellow",
                    text,
                    SelectedDefect.CX,
                    SelectedDefect.CY,
                    18
                ));*/

                ShowHRoi();
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }
        private void ClearHighlightRoi()
        {
            // mHRoi 是 ModuleBase 里维护的 ROI 列表（你项目里一定有）
            if (mHRoi == null || mHRoi.Count == 0)
                return;

            // 找到需要删除的高亮 ROI
            var toRemove = mHRoi
                .Where(r =>
                    (r.ModuleName == "SelectedDefect" || r.ModuleName == "SelectedText"))
                .ToList();

            foreach (var roi in toRemove)
            {
                // ✅ 释放 Halcon 对象
                try
                {
                    roi.hobject?.Dispose();   // 关键：释放真正占内存的 Halcon 资源
                }
                catch { }
                mHRoi.Remove(roi);
            }
        }



        [NonSerialized]
        private CommandBase _AiInit;
        public CommandBase AiInit
        {
            get
            {
                if (_AiInit == null)
                {
                    _AiInit = new CommandBase(obj =>
                    {
                        // 此时 Enable 已经被 ToggleButton 改成最新值
                        bool flag = SMFD.AiInit_dll(Enable, AiPath);

                        if (!flag)
                        {
                            Logger.AddLog(
                                Enable ? "打开AI模型失败！" : "关闭AI模型失败！",
                                eMsgType.Error);

                            // ⭐ 关键：失败时回滚 UI 状态
                            Enable = !Enable;
                        }
                    });
                }
                return _AiInit;
            }
        }
        //private bool _IsChecked = false;
        //public bool IsChecked
        //{
        //    get { return _IsChecked; }
        //    set { Set(ref _IsChecked, value); }
        //}
        ////TODO
        //[NonSerialized]
        //private CommandBase _AiInit;
        //public CommandBase AiInit
        //{
        //    get
        //    {
        //        if (_AiInit == null)
        //        {
        //            _AiInit = new CommandBase((obj) =>
        //            {
        //                //打开AI
        //                if (Enable)
        //                {
        //                    bool flag = SMFD.AiInit_dll(Enable, AiPath);
        //                    if (!flag)
        //                    {
        //                        Logger.AddLog("打开AI模型失败！", eMsgType.Error);
        //                    }
        //                    else {
        //                        IsChecked = true;
        //                    }
        //                }
        //                else {
        //                    bool flag = SMFD.AiInit_dll(Enable, AiPath);
        //                    if (!flag)
        //                    {
        //                        Logger.AddLog("关闭AI模型失败！", eMsgType.Error);
        //                    }
        //                    else {
        //                        IsChecked = false;
        //                    }
        //                }
        //            });
        //        }
        //        return _AiInit;
        //    }
        //}
        #endregion

        #region Method
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as AiView;
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
            var view = ModuleView as AiView;
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

            var view = ModuleView as AiView;
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
