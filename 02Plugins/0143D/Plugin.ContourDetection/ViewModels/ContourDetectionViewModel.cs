using EventMgrLib;
using HalconDotNet;
using Plugin.ContourDetection.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;
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
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;

namespace Plugin.ContourDetection.ViewModels
{
    #region Enums

    public enum eDetectionType
    {
        单条轮廓,
        连续轮廓,
        链接路径,
        轨迹编辑,
        逐行检测
    }

    public enum ePointType
    {
        Z最大值,
        拐点,
        D平均值,
        Z最小值,
        Z平均值
    }

    public enum eWorkflowCategory
    {
        构建,
        测量,
        计算
    }

    public enum eLinkCommand
    {
        InputImageLink,
        RoiCenterRow,
        RoiCenterCol,
        RoiLength1,
        RoiLength2,
        RoiPhi,
        ContourLength
    }

    public enum eEdgeShape
    {
        上升沿,
        下降沿
    }

    public enum eEdgeSelect
    {
        第一个,
        最后一个
    }

    public enum eARoiMode
    {
        固定位置,
        整体跟随
    }

    #endregion

    #region Models

    [Serializable]
    public class WorkflowItem : NotifyPropertyBase
    {
        private bool _m_enable = true;
        public bool m_enable
        {
            get { return _m_enable; }
            set { Set(ref _m_enable, value); }
        }

        private eWorkflowCategory _Category = eWorkflowCategory.构建;
        public eWorkflowCategory Category
        {
            get { return _Category; }
            set { Set(ref _Category, value); }
        }

        private string _OperationName = "点";
        public string OperationName
        {
            get { return _OperationName; }
            set
            {
                Set(ref _OperationName, value);
                RaisePropertyChanged(nameof(DisplayText));
            }
        }

        private int _Order;
        public int Order
        {
            get { return _Order; }
            set { Set(ref _Order, value); }
        }

        private ePointType _PointType = ePointType.Z最大值;
        public ePointType PointType
        {
            get { return _PointType; }
            set
            {
                Set(ref _PointType, value);
                RaisePropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        /// DataGrid参数列显示文本
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (OperationName == "点")
                    return $"点 | {PointType}";
                return OperationName;
            }
        }

        private InflectionParams _InflectionParams;
        public InflectionParams InflectionParams
        {
            get
            {
                if (_InflectionParams == null)
                    _InflectionParams = new InflectionParams();
                return _InflectionParams;
            }
            set { Set(ref _InflectionParams, value); }
        }
    }

    public class ContourStripData
    {
        public double[] PointY;
        public double[] PointX;
        public double[] ZValues;
        public double ResultRow;
        public double ResultCol;
        public double ResultValue;
        public double CenterRow;
        public double CenterCol;
        public double Phi;
        public double Length1;
        public double HalfWidth;
    }

    public class ContourResultItem : NotifyPropertyBase
    {
        private int _Index;
        public int Index
        {
            get { return _Index; }
            set { Set(ref _Index, value); }
        }

        private double _Value;
        public double Value
        {
            get { return _Value; }
            set { Set(ref _Value, value); }
        }

        private double _Row;
        public double Row
        {
            get { return _Row; }
            set { Set(ref _Row, value); }
        }

        private double _Col;
        public double Col
        {
            get { return _Col; }
            set { Set(ref _Col, value); }
        }
    }

    [Serializable]
    public class InflectionParams : NotifyPropertyBase
    {
        private eEdgeShape _Shape = eEdgeShape.上升沿;
        public eEdgeShape Shape
        {
            get { return _Shape; }
            set { Set(ref _Shape, value); }
        }

        private eEdgeSelect _Select = eEdgeSelect.第一个;
        public eEdgeSelect Select
        {
            get { return _Select; }
            set { Set(ref _Select, value); }
        }

        private double _Sensitivity = 0.1;
        public double Sensitivity
        {
            get { return _Sensitivity; }
            set { Set(ref _Sensitivity, value); }
        }

        private eARoiMode _ARoiMode = eARoiMode.固定位置;
        public eARoiMode ARoiMode
        {
            get { return _ARoiMode; }
            set
            {
                Set(ref _ARoiMode, value);
                RaisePropertyChanged(nameof(IsFollowModeVisible));
            }
        }

        public bool IsFollowModeVisible
        {
            get { return ARoiMode == eARoiMode.整体跟随; }
        }

        private string _Follow1;
        public string Follow1
        {
            get { return _Follow1; }
            set { Set(ref _Follow1, value); }
        }

        private double _Offset1;
        public double Offset1
        {
            get { return _Offset1; }
            set { Set(ref _Offset1, value); }
        }

        private double _Width1 = 10;
        public double Width1
        {
            get { return _Width1; }
            set { Set(ref _Width1, value); }
        }
    }

    #endregion

    [Category("3D")]
    [DisplayName("轮廓检测")]
    [ModuleImageName("ContourDetection")]
    [Serializable]
    public class ContourDetectionViewModel : ModuleBase
    {
        #region Overrides

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
                if (DispImage != null)
                    DispImage.mHRoi.RemoveAll(c => c.ModuleName == ModuleParam.ModuleName);
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

                if (DispImage.Type != "3D")
                {
                    Logger.AddLog("输入数据类型非3D！", eMsgType.Error);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 提取高度图
                HImage heightImage;
                int channels = DispImage.CountChannels();
                if (channels == 2)
                {
                    heightImage = DispImage.Decompose2(out HImage grayImage);
                }
                else
                {
                    heightImage = DispImage;
                }

                // 获取ROI参数（旋转矩形）
                double roiCenterRow = Convert.ToDouble(GetLinkValue(RoiCenterRow));
                double roiCenterCol = Convert.ToDouble(GetLinkValue(RoiCenterCol));
                double roiLength1 = Convert.ToDouble(GetLinkValue(RoiLength1));
                double roiLength2 = Convert.ToDouble(GetLinkValue(RoiLength2));
                double roiPhi = Convert.ToDouble(GetLinkValue(RoiPhi));

                // 显示ROI区域
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(roiCenterRow, roiCenterCol, -roiPhi, roiLength1, roiLength2);
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                    HRoiType.检测范围, "blue", new HObject(roiRegion)));

                // 根据检测类型分发
                switch (DetectionType)
                {
                    case eDetectionType.连续轮廓:
                        ExeContinuousContour(heightImage, roiCenterRow, roiCenterCol, roiLength1, roiLength2, roiPhi);
                        break;
                    case eDetectionType.单条轮廓:
                        ExeSingleContour(heightImage, roiCenterRow, roiCenterCol, roiLength1, roiLength2, roiPhi);
                        break;
                    case eDetectionType.链接路径:
                        // TODO: 待后续实现
                        break;
                    case eDetectionType.轨迹编辑:
                        // TODO: 待后续实现
                        break;
                    case eDetectionType.逐行检测:
                        // TODO: 待后续实现
                        break;
                }

                ShowHRoi();

                // 执行完成后自动应用当前索引的高亮和子窗口散点图
                UpdateContourHighlight();
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

        public bool ExeModuleROI()
        {
            Stopwatch.Restart();

            try
            {
                ClearRoiAndText();
                if (DispImage != null)
                    DispImage.mHRoi.RemoveAll(c => c.ModuleName == ModuleParam.ModuleName);
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                //GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                if (DispImage.Type != "3D")
                {
                    Logger.AddLog("输入数据类型非3D！", eMsgType.Error);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 提取高度图
                HImage heightImage;
                int channels = DispImage.CountChannels();
                if (channels == 2)
                {
                    heightImage = DispImage.Decompose2(out HImage grayImage);
                }
                else
                {
                    heightImage = DispImage;
                }

                // 获取ROI参数（旋转矩形）
                double roiCenterRow = Convert.ToDouble(GetLinkValue(RoiCenterRow));
                double roiCenterCol = Convert.ToDouble(GetLinkValue(RoiCenterCol));
                double roiLength1 = Convert.ToDouble(GetLinkValue(RoiLength1));
                double roiLength2 = Convert.ToDouble(GetLinkValue(RoiLength2));
                double roiPhi = Convert.ToDouble(GetLinkValue(RoiPhi));

                // 显示ROI区域
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(roiCenterRow, roiCenterCol, -roiPhi, roiLength1, roiLength2);
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                    HRoiType.检测范围, "cyan", new HObject(roiRegion)));

                // 根据检测类型分发
                switch (DetectionType)
                {
                    case eDetectionType.连续轮廓:
                        ExeContinuousContour(heightImage, roiCenterRow, roiCenterCol, roiLength1, roiLength2, roiPhi);
                        break;
                    case eDetectionType.单条轮廓:
                        ExeSingleContour(heightImage, roiCenterRow, roiCenterCol, roiLength1, roiLength2, roiPhi);
                        break;
                    case eDetectionType.链接路径:
                        // TODO: 待后续实现
                        break;
                    case eDetectionType.轨迹编辑:
                        // TODO: 待后续实现
                        break;
                    case eDetectionType.逐行检测:
                        // TODO: 待后续实现
                        break;
                }

                ShowHRoi(true);

                // 执行完成后自动应用当前索引的高亮和子窗口散点图
                UpdateContourHighlight();
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
            this.Prj.ClearOutputParam(this.ModuleParam);
            if (ContourResults.Count > 0)
            {
                for (int i = 0; i < ContourResults.Count; i++)
                {
                    AddOutputParam($"轮廓{i + 1}_值", "double", ContourResults[i].Value);
                    AddOutputParam($"轮廓{i + 1}_Row", "double", ContourResults[i].Row);
                    AddOutputParam($"轮廓{i + 1}_Col", "double", ContourResults[i].Col);
                }
                AddOutputParam("检测点_值", "double[]", ContourResults.Select(r => r.Value).ToList());
                AddOutputParam("检测点_Row", "double[]", ContourResults.Select(r => r.Row).ToList());
                AddOutputParam("检测点_Col", "double[]", ContourResults.Select(r => r.Col).ToList());
            }
            AddOutputParam("轮廓数量", "int", ContourResults.Count);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        public override void Loaded()
        {
            IsLoad = true;
            base.Loaded();
            var view = ModuleView as ContourDetectionView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (view.SubWindowH == null)
                {
                    view.SubWindowH = new VMHWindowControl();
                    view.winFormHostSub.Child = view.SubWindowH;
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
                    InitRoiMethod();
                }
                RoiCenterRow.TextChanged = new Action(() => { InitRoiChanged(); });
                RoiCenterCol.TextChanged = new Action(() => { InitRoiChanged(); });
                RoiLength1.TextChanged = new Action(() => { InitRoiChanged(); });
                RoiLength2.TextChanged = new Action(() => { InitRoiChanged(); });
                RoiPhi.TextChanged = new Action(() => { InitRoiChanged(); });
            }
            IsLoad = false;
        }

        #endregion

        #region 序列化修复

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            _ContourResults = new ObservableCollection<ContourResultItem>();
            _contourStripDataMap = new Dictionary<int, ContourStripData>();
            _contourRectMap = new Dictionary<int, HObject>();
        }
        //_WorkflowItems = new ObservableCollection<WorkflowItem>();

        #endregion

        #region Properties - Basic Parameters

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized() && !IsLoad)
                {
                    ShowHRoi();
                }
            }
        }

        private eDetectionType _DetectionType = eDetectionType.连续轮廓;
        public eDetectionType DetectionType
        {
            get { return _DetectionType; }
            set
            {
                Set(ref _DetectionType, value, () =>
                {
                    RaisePropertyChanged(nameof(IsContinuousContourVisible));
                });
            }
        }

        private LinkVarModel _RoiCenterRow = new LinkVarModel() { Text = "100" };
        public LinkVarModel RoiCenterRow
        {
            get { return _RoiCenterRow; }
            set { _RoiCenterRow = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _RoiCenterCol = new LinkVarModel() { Text = "100" };
        public LinkVarModel RoiCenterCol
        {
            get { return _RoiCenterCol; }
            set { _RoiCenterCol = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _RoiLength1 = new LinkVarModel() { Text = "50" };
        public LinkVarModel RoiLength1
        {
            get { return _RoiLength1; }
            set { _RoiLength1 = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _RoiLength2 = new LinkVarModel() { Text = "100" };
        public LinkVarModel RoiLength2
        {
            get { return _RoiLength2; }
            set { _RoiLength2 = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _RoiPhi = new LinkVarModel() { Text = "0" };
        public LinkVarModel RoiPhi
        {
            get { return _RoiPhi; }
            set { _RoiPhi = value; RaisePropertyChanged(); }
        }

        private int _ContourWidth = 10;
        public int ContourWidth
        {
            get { return _ContourWidth; }
            set { Set(ref _ContourWidth, value); }
        }

        private LinkVarModel _ContourLength = new LinkVarModel() { Text = "0" };
        public LinkVarModel ContourLength
        {
            get { return _ContourLength; }
            set { _ContourLength = value; RaisePropertyChanged(); }
        }

        private int _ContourInterval = 0;
        public int ContourInterval
        {
            get { return _ContourInterval; }
            set { Set(ref _ContourInterval, value); }
        }

        private bool _IsLinkEnabled = false;
        public bool IsLinkEnabled
        {
            get { return _IsLinkEnabled; }
            set { Set(ref _IsLinkEnabled, value, () => RaisePropertyChanged(nameof(IsLinkDisabled))); }
        }

        public bool IsLinkDisabled
        {
            get { return !_IsLinkEnabled; }
        }

        private int _ContourIndex = 0;
        public int ContourIndex
        {
            get { return _ContourIndex; }
            set { Set(ref _ContourIndex, value, UpdateContourHighlight); }
        }

        private int _MaxContourIndex = 0;
        public int MaxContourIndex
        {
            get { return _MaxContourIndex; }
            set { Set(ref _MaxContourIndex, value); }
        }

        private bool _IsFixedInterval = false;
        public bool IsFixedInterval
        {
            get { return _IsFixedInterval; }
            set
            {
                Set(ref _IsFixedInterval, value);
                RaisePropertyChanged(nameof(IsIntervalSamplingVisible));
                RaisePropertyChanged(nameof(IsFixedCountVisible));
            }
        }

        private int _ContourCount = 10;
        public int ContourCount
        {
            get { return _ContourCount; }
            set { Set(ref _ContourCount, value); }
        }

        public bool IsIntervalSamplingVisible
        {
            get { return !_IsFixedInterval; }
        }

        public bool IsFixedCountVisible
        {
            get { return _IsFixedInterval; }
        }

        /// <summary>
        /// 控制"连续轮廓"GroupBox的可见性：仅在检测类型为"连续轮廓"时显示
        /// </summary>
        public bool IsContinuousContourVisible
        {
            get { return DetectionType == eDetectionType.连续轮廓; }
        }

        #endregion

        #region Properties - Parameter Settings

        // [NonSerialized]
        private ObservableCollection<WorkflowItem> _WorkflowItems = new ObservableCollection<WorkflowItem>();
        public ObservableCollection<WorkflowItem> WorkflowItems
        {
            get { return _WorkflowItems; }
            set { _WorkflowItems = value; RaisePropertyChanged(); }
        }

        [NonSerialized]
        private int _SelectedIndex;
        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set { Set(ref _SelectedIndex, value); }
        }

        [NonSerialized]
        private WorkflowItem _SelectedWorkflowItem;
        public WorkflowItem SelectedWorkflowItem
        {
            get { return _SelectedWorkflowItem; }
            set
            {
                if (_SelectedWorkflowItem != null)
                    _SelectedWorkflowItem.PropertyChanged -= OnSelectedItemPropertyChanged;
                Set(ref _SelectedWorkflowItem, value);
                if (_SelectedWorkflowItem != null)
                    _SelectedWorkflowItem.PropertyChanged += OnSelectedItemPropertyChanged;
                RaisePropertyChanged(nameof(IsInflectionPointSelected));
                RaisePropertyChanged(nameof(FollowOptions));
            }
        }

        private void OnSelectedItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorkflowItem.PointType))
            {
                RaisePropertyChanged(nameof(IsInflectionPointSelected));
            }
        }

        public Array PointTypeOptions
        {
            get { return Enum.GetValues(typeof(ePointType)); }
        }

        public Array EdgeShapeOptions
        {
            get { return Enum.GetValues(typeof(eEdgeShape)); }
        }

        public Array EdgeSelectOptions
        {
            get { return Enum.GetValues(typeof(eEdgeSelect)); }
        }

        public Array ARoiModeOptions
        {
            get { return Enum.GetValues(typeof(eARoiMode)); }
        }

        /// <summary>
        /// 当前选中项是否为拐点类型，控制拐点参数UI显隐
        /// </summary>
        public bool IsInflectionPointSelected
        {
            get { return SelectedWorkflowItem != null && SelectedWorkflowItem.PointType == ePointType.拐点; }
        }

        /// <summary>
        /// 跟随1可选对象列表：当前工作流中除自身外的启用项
        /// </summary>
        public IEnumerable<string> FollowOptions
        {
            get
            {
                if (SelectedWorkflowItem == null)
                    return Enumerable.Empty<string>();
                return WorkflowItems
                    .Where(item => item != SelectedWorkflowItem && item.m_enable)
                    .Select(item => item.DisplayText);
            }
        }

        private ePointType _SelectedPointType = ePointType.Z最大值;
        public ePointType SelectedPointType
        {
            get { return _SelectedPointType; }
            set { Set(ref _SelectedPointType, value); }
        }

        #endregion

        #region Properties - Data Results

        [NonSerialized]
        private ObservableCollection<ContourResultItem> _ContourResults = new ObservableCollection<ContourResultItem>();
        public ObservableCollection<ContourResultItem> ContourResults
        {
            get { return _ContourResults; }
            set { _ContourResults = value; RaisePropertyChanged(); }
        }

        #endregion

        #region Properties - Display Settings

        private int _ShowMaxPoints = 1000;
        public int ShowMaxPoints
        {
            get { return _ShowMaxPoints; }
            set { Set(ref _ShowMaxPoints, value); }
        }

        private bool _ShowResultPoints = true;
        public bool ShowResultPoints
        {
            get { return _ShowResultPoints; }
            set { Set(ref _ShowResultPoints, value); }
        }

        private bool _ShowResultRois = true;
        public bool ShowResultRois
        {
            get { return _ShowResultRois; }
            set { Set(ref _ShowResultRois, value); }
        }
        #endregion

        #region Fields

        private bool IsLoad = false;
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();

        /// <summary>
        /// 存储每条小轮廓的完整原始数据（按 localOffsets 索引）
        /// </summary>
        [NonSerialized]
        private Dictionary<int, ContourStripData> _contourStripDataMap = new Dictionary<int, ContourStripData>();

        /// <summary>
        /// 存储每条小轮廓的矩形 HObject（用于主窗口黄色高亮）
        /// </summary>
        [NonSerialized]
        private Dictionary<int, HObject> _contourRectMap = new Dictionary<int, HObject>();

        #endregion

        #region Commands

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.RoiCenterRow:
                    RoiCenterRow.Text = obj.LinkName;
                    break;
                case eLinkCommand.RoiCenterCol:
                    RoiCenterCol.Text = obj.LinkName;
                    break;
                case eLinkCommand.RoiLength1:
                    RoiLength1.Text = obj.LinkName;
                    break;
                case eLinkCommand.RoiLength2:
                    RoiLength2.Text = obj.LinkName;
                    break;
                case eLinkCommand.RoiPhi:
                    RoiPhi.Text = obj.LinkName;
                    break;
                case eLinkCommand.ContourLength:
                    ContourLength.Text = obj.LinkName;
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
                            case eLinkCommand.RoiCenterRow:
                            case eLinkCommand.RoiCenterCol:
                            case eLinkCommand.RoiLength1:
                            case eLinkCommand.RoiLength2:
                            case eLinkCommand.RoiPhi:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},{linkCommand}");
                                break;
                            case eLinkCommand.ContourLength:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},ContourLength");
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
                        var view = this.ModuleView as ContourDetectionView;
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
        private CommandBase _DataOperateCommand;
        public CommandBase DataOperateCommand
        {
            get
            {
                if (_DataOperateCommand == null)
                {
                    _DataOperateCommand = new CommandBase((obj) =>
                    {
                        string[] sArray = obj.ToString().Split('_');
                        if (sArray.Length == 3 && sArray[0] == "add")
                        {
                            WorkflowItems.Add(new WorkflowItem
                            {
                                m_enable = true,
                                Category = (eWorkflowCategory)Enum.Parse(typeof(eWorkflowCategory), sArray[1]),
                                OperationName = sArray[2],
                                Order = WorkflowItems.Count + 1
                            });
                        }
                        else
                        {
                            switch (sArray[0])
                            {
                                case "remove":
                                    if (SelectedWorkflowItem == null) return;
                                    WorkflowItems.Remove(SelectedWorkflowItem);
                                    break;
                                case "up":
                                    if (SelectedWorkflowItem == null) return;
                                    int i = WorkflowItems.IndexOf(SelectedWorkflowItem);
                                    if (i > 0)
                                        WorkflowItems.Move(i, i - 1);
                                    break;
                                case "down":
                                    if (SelectedWorkflowItem == null) return;
                                    int j = WorkflowItems.IndexOf(SelectedWorkflowItem);
                                    if (j + 1 < WorkflowItems.Count)
                                        WorkflowItems.Move(j, j + 1);
                                    break;
                                default:
                                    break;
                            }
                        }
                        SelectedIndex = WorkflowItems.Count - 1;
                    });
                }
                return _DataOperateCommand;
            }
        }

        #endregion

        #region Methods - ROI Interaction

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as ContourDetectionView;
                if (view == null) return;

                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length < 1) return;

                if (roi is ROIRectangle2Custom rect2Custom)
                {
                    RoiList[index] = roi;

                    if (!RoiCenterRow.Text.StartsWith("&"))
                        RoiCenterRow.Text = Math.Round(rect2Custom.MidR, 3).ToString();
                    if (!RoiCenterCol.Text.StartsWith("&"))
                        RoiCenterCol.Text = Math.Round(rect2Custom.MidC, 3).ToString();
                    if (!RoiLength1.Text.StartsWith("&"))
                        RoiLength1.Text = Math.Round(rect2Custom.Length1, 3).ToString();
                    if (!RoiLength2.Text.StartsWith("&"))
                        RoiLength2.Text = Math.Round(rect2Custom.Length2, 3).ToString();
                    if (!RoiPhi.Text.StartsWith("&"))
                        RoiPhi.Text = Math.Round(rect2Custom.Phi, 4).ToString();

                    view.mWindowH.WindowH.genRect2Custom(ModuleParam.ModuleName + ROIDefine.Rectangle2,
                        rect2Custom.MidR, rect2Custom.MidC, rect2Custom.Phi,
                        rect2Custom.Length1, rect2Custom.Length2, ref RoiList);
                    ExeModuleROI();
                }
            }
            catch (Exception)
            {
            }
        }

        private void InitRoiChanged()
        {
            var view = ModuleView as ContourDetectionView;
            if (view == null) return;

            double centerRow = Convert.ToDouble(GetLinkValue(RoiCenterRow));
            double centerCol = Convert.ToDouble(GetLinkValue(RoiCenterCol));
            double length1 = Convert.ToDouble(GetLinkValue(RoiLength1));
            double length2 = Convert.ToDouble(GetLinkValue(RoiLength2));
            double phi = Convert.ToDouble(GetLinkValue(RoiPhi));

            if (!RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle2))
                return;

            if (view.mWindowH == null || DispImage == null || !DispImage.IsInitialized())
                return;

            // 清除叠加层(旧HRoi)，重绘图像但保留当前缩放/平移状态
            // displayImageWithoutFit 不调用 ResetWindow，因此不触发 fit-to-window
            // 3D/real 类型图像需先转换为 byte 才能正常显示（与 ChangeEnable 逻辑一致）
            view.mWindowH.WindowH.ClearWindow();
            if (DispImage.GetImageType() == "byte")
                view.mWindowH.WindowH.displayImageWithoutFit(DispImage);
            else
                view.mWindowH.WindowH.displayImageWithoutFit(TR.GetRGBImage(DispImage));

            // 重建ROI交互手柄
            view.mWindowH.WindowH.genRect2Custom(ModuleParam.ModuleName + ROIDefine.Rectangle2,
                centerRow, centerCol, phi, length1, length2, ref RoiList);
        }

        public void InitRoiMethod()
        {
            var view = ModuleView as ContourDetectionView;
            if (view == null) return;

            if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle2))
            {
                ROIRectangle2Custom roiRect = (ROIRectangle2Custom)RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle2];
                view.mWindowH.WindowH.genRect2Custom(ModuleParam.ModuleName + ROIDefine.Rectangle2,
                    roiRect.MidR, roiRect.MidC, roiRect.Phi, roiRect.Length1, roiRect.Length2, ref RoiList);
            }
            else
            {
                if (DispImage != null && DispImage.IsInitialized())
                {
                    int imgWidth, imgHeight;
                    DispImage.GetImageSize(out imgWidth, out imgHeight);
                    double defaultLength1 = Math.Min(50, imgHeight / 4);
                    double defaultLength2 = Math.Min(100, imgWidth / 4);
                    view.mWindowH.WindowH.genRect2Custom(ModuleParam.ModuleName + ROIDefine.Rectangle2,
                        imgHeight / 2.0, imgWidth / 2.0, 0, defaultLength1, defaultLength2, ref RoiList);
                }
            }
        }

        #endregion

        #region Methods - Core Logic

        /// <summary>
        /// 连续轮廓检测
        /// </summary>
        private void ExeContinuousContour(HImage heightImage, double roiCenterRow, double roiCenterCol,
            double roiLength1, double roiLength2, double roiPhi)
        {
            // 使用本地List收集结果，避免在后台线程直接操作UI绑定的ObservableCollection
            List<ContourResultItem> results = new List<ContourResultItem>();
            int contourWidth = ContourWidth;

            if (contourWidth <= 0) return;

            // 没有启用的工作流项，无需切分和检测，直接返回
            if (!WorkflowItems.Any(item => item.m_enable)) return;

            // 有效检测长度 = roiLength2 * 2，同步到ContourLength
            if (!ContourLength.Text.StartsWith("&"))
                ContourLength.Text = Math.Round(roiLength2 * 2, 3).ToString();

            // 局部坐标系到图像坐标系的变换
            double cosPhi = Math.Cos(roiPhi);
            double sinPhi = Math.Sin(roiPhi);

            // 计算每个小轮廓沿高度方向（Length2）的局部偏移量
            List<double> localOffsets = new List<double>();
            double halfWidth = contourWidth / 2.0;

            if (IsFixedInterval)
            {
                // 固定个数模式：在ROI高度方向上均匀分布
                int count = Math.Max(1, ContourCount);
                if (count == 1)
                {
                    localOffsets.Add(0);
                }
                else
                {
                    double availableRange = roiLength2 - halfWidth;
                    double spacing = (2.0 * availableRange) / (count - 1);
                    for (int i = 0; i < count; i++)
                    {
                        double localOffset = -availableRange + i * spacing;
                        if (localOffset + halfWidth <= roiLength2 && localOffset - halfWidth >= -roiLength2)
                            localOffsets.Add(localOffset);
                    }
                }
            }
            else
            {
                // 间隔采样模式：从ROI中心开始双向扩展
                localOffsets.Add(0); // 中心为第一个
                int maxSteps = ContourInterval > 0
                    ? (int)Math.Ceiling((roiLength2 - halfWidth) / ContourInterval)
                    : 0;
                for (int step = 1; step <= maxSteps; step++)
                {
                    double posOffset = step * ContourInterval;
                    double negOffset = -step * ContourInterval;
                    bool added = false;

                    if (posOffset + halfWidth <= roiLength2)
                    {
                        localOffsets.Add(posOffset);
                        added = true;
                    }
                    if (negOffset - halfWidth >= -roiLength2)
                    {
                        localOffsets.Add(negOffset);
                        added = true;
                    }
                    if (!added) break;
                }
                localOffsets.Sort();
            }

            // 累计小轮廓矩形和检测点
            HObject allContourRegions = new HObject();
            allContourRegions.GenEmptyObj();
            HObject allCrosses = new HObject();
            allCrosses.GenEmptyObj();

            // 清除上次保存的小轮廓数据
            _contourStripDataMap.Clear();
            foreach (var kv in _contourRectMap)
            {
                if (kv.Value != null) kv.Value.Dispose();
            }
            _contourRectMap.Clear();

            for (int i = 0; i < localOffsets.Count; i++)
            {
                double localOffset = localOffsets[i];

                // 变换到图像坐标系（沿高度/Length2方向：cosPhi, -sinPhi）
                double stripeCenterRow = roiCenterRow + localOffset * cosPhi;
                double stripeCenterCol = roiCenterCol - localOffset * sinPhi;

                // 累计轮廓矩形（用于绘制，颜色与ROI一致）
                HObject contourRect;
                HOperatorSet.GenRectangle2ContourXld(out contourRect, stripeCenterRow, stripeCenterCol,
                    -roiPhi, roiLength1, halfWidth);
                HOperatorSet.ConcatObj(allContourRegions, contourRect, out allContourRegions);

                // 保存轮廓矩形（用于后续索引高亮）
                _contourRectMap[i] = contourRect.Clone();

                // 生成旋转的条纹区域
                HRegion subRegion = new HRegion();
                subRegion.GenRectangle2(stripeCenterRow, stripeCenterCol, -roiPhi, roiLength1, halfWidth);

                // ReduceDomain获取该小轮廓区域的数据
                HImage reducedImage = heightImage.ReduceDomain(subRegion);
                HRegion domain = reducedImage.GetDomain();
                domain.GetRegionPoints(out HTuple pointY, out HTuple pointX);

                if (pointY.Length == 0)
                {
                    subRegion.Dispose();
                    reducedImage.Dispose();
                    domain.Dispose();

                    results.Add(new ContourResultItem
                    {
                        Index = i + 1,
                        Value = double.NaN,
                        Row = stripeCenterRow,
                        Col = stripeCenterCol
                    });
                    continue;
                }

                HTuple zValues = reducedImage.GetGrayval(pointY, pointX);

                // 工作流分发
                double resultVal = 0;
                double resultRow = stripeCenterRow;
                double resultCol = stripeCenterCol;
                foreach (var item in WorkflowItems)
                {
                    if (!item.m_enable) continue;

                    switch (item.Category)
                    {
                        case eWorkflowCategory.构建:
                            var (val, row, col) = ContourDetectionModel.ExeBuildWorkflow(item, zValues, pointY, pointX);
                            resultVal = val;
                            if (row != 0 || col != 0)
                            {
                                resultRow = row;
                                resultCol = col;
                            }
                            break;
                        case eWorkflowCategory.测量:
                            // TODO: 待后续实现
                            break;
                        case eWorkflowCategory.计算:
                            // TODO: 待后续实现
                            break;
                    }
                }

                results.Add(new ContourResultItem
                {
                    Index = i + 1,
                    Value = resultVal,
                    Row = resultRow,
                    Col = resultCol
                });

                // 保存小轮廓原始数据（用于索引高亮和子窗口过滤显示）
                _contourStripDataMap[i] = new ContourStripData
                {
                    PointY = pointY.ToDArr(),
                    PointX = pointX.ToDArr(),
                    ZValues = zValues.ToDArr(),
                    ResultRow = resultRow,
                    ResultCol = resultCol,
                    ResultValue = resultVal,
                    CenterRow = stripeCenterRow,
                    CenterCol = stripeCenterCol,
                    Phi = -roiPhi,
                    Length1 = roiLength1,
                    HalfWidth = halfWidth
                };

                subRegion.Dispose();
                reducedImage.Dispose();
                domain.Dispose();
            }

            // 同步结果到UI线程的ObservableCollection，过滤NaN值
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ContourResults.Clear();
                foreach (var item in results)
                {
                    if (!double.IsNaN(item.Value))
                        ContourResults.Add(item);
                }
            });

            // 绘制检测点，过滤NaN值
            if (ShowResultPoints)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    if (double.IsNaN(results[i].Value)) continue;
                    HObject cross;
                    HOperatorSet.GenCrossContourXld(out cross, results[i].Row, results[i].Col, 6, 0.785398);
                    HOperatorSet.ConcatObj(allCrosses, cross, out allCrosses);
                }
                if (allCrosses.CountObj() > 0)
                {
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                        HRoiType.检测点, "red", new HObject(allCrosses)));
                }
            }

            // 绘制小轮廓矩形框（与ROI颜色一致）
            if (ShowResultRois && allContourRegions.CountObj() > 0)
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                    HRoiType.检测结果, "cyan", new HObject(allContourRegions)));
            }

            // 更新最大索引值（用于UI绑定Maximum）
            MaxContourIndex = localOffsets.Count - 1;
        }

        /// <summary>
        /// 单条轮廓检测：仅生成 ROI 中心位置的一条轮廓条带
        /// </summary>
        private void ExeSingleContour(HImage heightImage, double roiCenterRow, double roiCenterCol,
            double roiLength1, double roiLength2, double roiPhi)
        {
            List<ContourResultItem> results = new List<ContourResultItem>();
            int contourWidth = ContourWidth;

            if (contourWidth <= 0) return;
            if (!WorkflowItems.Any(item => item.m_enable)) return;

            double halfWidth = contourWidth / 2.0;

            // 清除上次保存的数据
            _contourStripDataMap.Clear();
            foreach (var kv in _contourRectMap)
            {
                if (kv.Value != null) kv.Value.Dispose();
            }
            _contourRectMap.Clear();

            // 调用模型处理单条中心条带
            var (resultVal, resultRow, resultCol, pointY, pointX, zValues, hasData) =
                ContourDetectionModel.ProcessSingleStrip(
                    heightImage, roiCenterRow, roiCenterCol, -roiPhi,
                    roiLength1, halfWidth,
                    WorkflowItems.Where(item => item.m_enable));

            // 保存轮廓矩形（用于主窗口黄色高亮）
            HObject contourRectObj;
            HOperatorSet.GenRectangle2ContourXld(out contourRectObj,
                roiCenterRow, roiCenterCol, -roiPhi, roiLength1, halfWidth);
            _contourRectMap[0] = contourRectObj.Clone();

            if (!hasData)
            {
                results.Add(new ContourResultItem
                {
                    Index = 1,
                    Value = double.NaN,
                    Row = roiCenterRow,
                    Col = roiCenterCol
                });
            }
            else
            {
                results.Add(new ContourResultItem
                {
                    Index = 1,
                    Value = resultVal,
                    Row = resultRow,
                    Col = resultCol
                });

                // 按 Col 聚合数据（消除多行影响），用于子窗口散点图
                var (aggCols, aggRows, aggZ) = ContourDetectionModel.AggregateByCol(pointX, pointY, zValues);
                // ---- 调试：导出原始点云数据（x y z） ----
                using (var sw = System.IO.File.CreateText(@"D:\ContourDebug_Schmitt_Show.txt"))
                {
                    for (int i = 0; i < aggZ.Length; i++)
                        sw.WriteLine($"{aggCols[i]:F8} {aggRows[i]:F8} {aggZ[i]:F8}");
                }
                // ---- 调试结束 ----

                // 保存聚合后的条带数据（用于子窗口散点图）
                _contourStripDataMap[0] = new ContourStripData
                {
                    PointY = aggRows,
                    PointX = aggCols,
                    ZValues = aggZ,
                    ResultRow = resultRow,
                    ResultCol = resultCol,
                    ResultValue = resultVal,
                    CenterRow = roiCenterRow,
                    CenterCol = roiCenterCol,
                    Phi = -roiPhi,
                    Length1 = roiLength1,
                    HalfWidth = halfWidth
                };
            }

            // 同步结果到UI
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ContourResults.Clear();
                foreach (var item in results)
                {
                    if (!double.IsNaN(item.Value))
                        ContourResults.Add(item);
                }
            });

            // 绘制检测点（红色十字）
            if (ShowResultPoints && hasData && !double.IsNaN(resultVal))
            {
                HObject cross;
                HOperatorSet.GenCrossContourXld(out cross, resultRow, resultCol, 6, 0.785398);
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                    HRoiType.检测点, "red", new HObject(cross)));
            }

            // 绘制轮廓矩形框
            if (ShowResultRois)
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                    HRoiType.检测结果, "cyan", new HObject(contourRectObj)));
            }

            // 只有一条轮廓，索引固定为0
            MaxContourIndex = 0;
        }

        /// <summary>
        /// 在子窗口中显示截面数据
        /// </summary>
        private void ShowSubWindow()
        {
            var view = ModuleView as ContourDetectionView;
            if (view == null || view.IsClosed) return;
            if (view.SubWindowH == null) return;

            try
            {
                view.SubWindowH.ClearWindow();
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.SubWindowH.HobjectToHimage(DispImage);
                }

                // 绘制检测点
                if (ShowResultPoints)
                {
                    List<HRoi> roiList = mHRoi.Where(c => c.roiType == HRoiType.检测点).ToList();
                    foreach (HRoi roi in roiList)
                    {
                        view.SubWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// ContourIndex 变化时更新主窗口黄色高亮和子窗口过滤显示
        /// </summary>
        private void UpdateContourHighlight()
        {
            var view = ModuleView as ContourDetectionView;
            if (view == null || view.IsClosed) return;

            // 索引超出范围则不做任何操作
            if (_contourStripDataMap == null || !_contourStripDataMap.ContainsKey(ContourIndex))
                return;

            // === 主窗口：黄色高亮选中索引的小轮廓 ===
            try
            {
                // 先正常渲染（包含所有 cyan 小轮廓）
                ShowHRoi(true);

                // 在主窗口叠加选中索引的黄色高亮
                if (_contourRectMap != null && _contourRectMap.ContainsKey(ContourIndex))
                {
                    var mainWin = view.mWindowH;
                    if (mainWin != null)
                    {
                        mainWin.WindowH.DispHobject(_contourRectMap[ContourIndex], "yellow");
                    }
                }
            }
            catch (Exception)
            {
            }

            // === 子窗口：只显示选中索引的检测点所在行数据（散点图） ===
            try
            {
                if (view.SubWindowH == null) return;

                var stripData = _contourStripDataMap[ContourIndex];
                if (stripData.PointY == null || stripData.PointY.Length == 0) return;
                if (double.IsNaN(stripData.ResultValue)) return;

                // 显示条带内所有有效数据点（Col-Z 散点图）
                List<double> filterCols = new List<double>();
                List<double> filterZ = new List<double>();

                for (int i = 0; i < stripData.PointX.Length; i++)
                {
                    if (!double.IsNaN(stripData.ZValues[i]))
                    {
                        filterCols.Add(stripData.PointX[i]);
                        filterZ.Add(stripData.ZValues[i]);
                    }
                }

                if (filterCols.Count == 0) return;

                // 最大点数采样：均匀下采样保留首尾+检测目标点，只降密度不裁剪范围
                if (filterCols.Count > ShowMaxPoints)
                {
                    int n = filterCols.Count;

                    // 找到检测目标点（红叉）在原始数据中的索引
                    int detIdx = 0;
                    double minDistSample = double.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        double dc = Math.Abs(filterCols[i] - stripData.ResultCol);
                        double dz = Math.Abs(filterZ[i] - stripData.ResultValue);
                        double dist = dc + dz;
                        if (dist < minDistSample) { minDistSample = dist; detIdx = i; }
                    }

                    // 必须保留的索引：首点、尾点、检测目标点
                    var keepSet = new HashSet<int> { 0, n - 1, detIdx };

                    // 剩余名额从其余位置均匀采样
                    int remaining = ShowMaxPoints - keepSet.Count;
                    if (remaining > 0)
                    {
                        double step = (double)(n - 1) / (remaining + 1);
                        for (int i = 1; i <= remaining; i++)
                        {
                            int idx = (int)Math.Round(i * step);
                            idx = Math.Max(0, Math.Min(n - 1, idx));
                            keepSet.Add(idx);
                        }
                    }

                    // 按原始顺序提取采样后数据
                    var sortedIndices = keepSet.OrderBy(i => i).ToList();
                    filterCols = sortedIndices.Select(i => filterCols[i]).ToList();
                    filterZ   = sortedIndices.Select(i => filterZ[i]).ToList();
                }

                // 计算数据范围
                double minCol = filterCols.Min();
                double maxCol = filterCols.Max();
                double minZ = filterZ.Min();
                double maxZ = filterZ.Max();
                double colRange = maxCol - minCol;
                double zRange = maxZ - minZ;
                if (colRange < 1) colRange = 1;
                if (zRange < 0.001) zRange = 0.001;

                // 创建黑色画布图像，通过 HobjectToHimage 初始化子窗口坐标系
                // 画布坐标系: Row 0..canvasH, Col 0..canvasW（图像像素坐标）
                int canvasW = 800;
                int canvasH = 600;
                HImage canvas = new HImage();
                canvas.GenImageConst("byte", canvasW, canvasH);
                view.SubWindowH.ClearWindow();
                view.SubWindowH.HobjectToHimage(canvas);
                canvas.Dispose();

                // 绘图区域（画布像素坐标，留边距给坐标轴和刻度文字）
                int margin = 60;
                double plotW = canvasW - 2 * margin;
                double plotH = canvasH - 2 * margin;

                // ========== 第一阶段：所有持久对象（DispHobject，Repaint 不丢失） ==========

                // --- X轴（底部水平线） ---
                HObject xAxis;
                HOperatorSet.GenContourPolygonXld(out xAxis,
                    new HTuple(new double[] { canvasH - margin, canvasH - margin }),
                    new HTuple(new double[] { margin, canvasW - margin }));
                view.SubWindowH.WindowH.DispHobject(xAxis, "white");
                xAxis.Dispose();

                // --- Y轴（左侧垂直线） ---
                HObject yAxis;
                HOperatorSet.GenContourPolygonXld(out yAxis,
                    new HTuple(new double[] { margin, canvasH - margin }),
                    new HTuple(new double[] { margin, margin }));
                view.SubWindowH.WindowH.DispHobject(yAxis, "white");
                yAxis.Dispose();

                // --- X轴刻度线 ---
                for (int t = 0; t <= 5; t++)
                {
                    double px = margin + t * plotW / 5;
                    HObject tick;
                    HOperatorSet.GenContourPolygonXld(out tick,
                        new HTuple(new double[] { canvasH - margin, canvasH - margin + 8 }),
                        new HTuple(new double[] { px, px }));
                    view.SubWindowH.WindowH.DispHobject(tick, "white");
                    tick.Dispose();
                }

                // --- Y轴刻度线 ---
                for (int t = 0; t <= 5; t++)
                {
                    double py = (canvasH - margin) - t * plotH / 5;
                    HObject tick;
                    HOperatorSet.GenContourPolygonXld(out tick,
                        new HTuple(new double[] { py, py }),
                        new HTuple(new double[] { margin - 8, margin }));
                    view.SubWindowH.WindowH.DispHobject(tick, "white");
                    tick.Dispose();
                }

                // --- 找到检测点在过滤列表中的索引 ---
                int detectionIdx = -1;
                double minDist = double.MaxValue;
                for (int i = 0; i < filterCols.Count; i++)
                {
                    double dc = Math.Abs(filterCols[i] - stripData.ResultCol);
                    double dz = Math.Abs(filterZ[i] - stripData.ResultValue);
                    if (dc + dz < minDist)
                    {
                        minDist = dc + dz;
                        detectionIdx = i;
                    }
                }

                // --- 数据点（绿色小十字） ---
                HTuple pointRows = new HTuple();
                HTuple pointCols = new HTuple();
                for (int i = 0; i < filterCols.Count; i++)
                {
                    double px = margin + (filterCols[i] - minCol) / colRange * plotW;
                    double py = (canvasH - margin) - (filterZ[i] - minZ) / zRange * plotH;
                    pointRows.Append(py);
                    pointCols.Append(px);
                }

                if (pointCols.Length > 0)
                {
                    HObject greenPoints;
                    HOperatorSet.GenCrossContourXld(out greenPoints, pointRows, pointCols, 4, 0.785398);
                    view.SubWindowH.WindowH.DispHobject(greenPoints, "green");
                    greenPoints.Dispose();
                }

                // --- 检测点（红色大十字） ---
                if (detectionIdx >= 0 && detectionIdx < filterCols.Count)
                {
                    double detPx = margin + (filterCols[detectionIdx] - minCol) / colRange * plotW;
                    double detPy = (canvasH - margin) - (filterZ[detectionIdx] - minZ) / zRange * plotH;
                    HObject detCross;
                    HOperatorSet.GenCrossContourXld(out detCross, detPy, detPx, 10, 0.785398);
                    view.SubWindowH.WindowH.DispHobject(detCross, "red");
                    detCross.Dispose();
                }

                // ========== 第二阶段：刻度文字（DispText 持久化到 roiTextList，缩放/平移不丢失） ==========
                try
                {
                    // ShowTool.SetMsg 内部参数约定：hv_Row(第4参数)=X(col), hv_Col(第5参数)=Y(row)
                    // HText 坐标约定与之一致：row=图像col(X), col=图像row(Y)
                    // 使用 DispText 方法将文字加入持久化列表，窗口重绘时自动恢复

                    // X轴刻度值（底部水平轴，col随刻度水平居中，row在轴下方紧贴）
                    for (int t = 0; t <= 5; t++)
                    {
                        double px = margin + t * plotW / 5;
                        double val = minCol + t * colRange / 5;
                        view.SubWindowH.WindowH.DispText(new HText("white",
                            Math.Round(val, 1).ToString(),
                            (int)px - 10,              // row = X(col): 刻度线水平居中
                            canvasH - margin + 14,      // col = Y(row): X轴下方紧贴
                            12));
                    }

                    // Y轴刻度值（左侧垂直轴，col在轴左侧紧贴，row随刻度垂直居中）
                    for (int t = 0; t <= 5; t++)
                    {
                        double py = (canvasH - margin) - t * plotH / 5;
                        double val = minZ + t * zRange / 5;
                        view.SubWindowH.WindowH.DispText(new HText("white",
                            Math.Round(val, 2).ToString(),
                            margin - 40,               // row = X(col): Y轴左侧紧贴
                            (int)py - 4,               // col = Y(row): 刻度线垂直居中
                            12));
                    }
                }
                catch (Exception)
                {
                    // 文字绘制失败不影响数据点和坐标轴显示
                }
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Methods - Display

        /// <summary>
        /// ROI改变后的专用渲染：清除旧叠加层、重绘图像但保留当前缩放/平移状态，
        /// 然后渲染全部HRoi结果。用于 ExeModuleTest 等不需要全图缩放的场景。
        /// </summary>
        public override void ShowHRoi()
        {
            ShowHRoi(false);
        }

        /// <param name="preserveZoom">true: 保留当前缩放/平移（ROI拖动时）; false: 自适应全图显示（正常执行/加载时）</param>
        public void ShowHRoi(bool preserveZoom)
        {
            var view = ModuleView as ContourDetectionView;
            VMHWindowControl mWindowH;
            bool dispSearchRegion = true;

            if (view == null || view.IsClosed)
            {
                if (preserveZoom) return;  // 保留缩放模式必须有活跃视图
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispSearchRegion = false;
            }
            else
            {
                mWindowH = view.mWindowH;
                if (mWindowH == null) return;

                if (preserveZoom && DispImage != null && DispImage.IsInitialized())
                {
                    // 保留缩放/平移：只清叠加层栈 + 无缩放重绘图像
                    mWindowH.WindowH.ClearWindow();
                    if (DispImage.GetImageType() == "byte")
                        mWindowH.WindowH.displayImageWithoutFit(DispImage);
                    else
                        mWindowH.WindowH.displayImageWithoutFit(TR.GetRGBImage(DispImage));
                }
                else
                {
                    // 正常模式：全图自适应显示
                    mWindowH.ClearWindow();
                    if (DispImage != null && DispImage.IsInitialized())
                    {
                        mWindowH.Image = new RImage(DispImage);
                    }
                }
            }

            // 重建ROI交互手柄（两种模式共用）
            if (dispSearchRegion && mWindowH != null)
            {
                if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle2))
                {
                    ROIRectangle2Custom roiRect = (ROIRectangle2Custom)RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle2];
                    mWindowH.WindowH.genRect2Custom(ModuleParam.ModuleName + ROIDefine.Rectangle2,
                        roiRect.MidR, roiRect.MidC, roiRect.Phi, roiRect.Length1, roiRect.Length2, ref RoiList);
                }
                else
                {
                    if (DispImage != null && DispImage.IsInitialized())
                    {
                        int imgWidth, imgHeight;
                        DispImage.GetImageSize(out imgWidth, out imgHeight);
                        double defaultLength1 = Math.Min(50, imgHeight / 4);
                        double defaultLength2 = Math.Min(100, imgWidth / 4);
                        mWindowH.WindowH.genRect2Custom(ModuleParam.ModuleName + ROIDefine.Rectangle2,
                            imgHeight / 2.0, imgWidth / 2.0, 0, defaultLength1, defaultLength2, ref RoiList);
                    }
                }
            }

            // 渲染所有HRoi叠加层（两种模式共用）
            List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            foreach (HRoi roi in roiList)
            {
                if (roi.roiType == HRoiType.文字显示)
                {
                    HText roiText = (HText)roi;
                    ShowTool.SetFont(mWindowH.hControl.HalconWindow, roiText.size, "false", "false");
                    ShowTool.SetMsg(mWindowH.hControl.HalconWindow, roiText.text, "image",
                        roiText.row, roiText.col, roiText.drawColor, "false");
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                }
            }
        }

        #endregion
    }
}
