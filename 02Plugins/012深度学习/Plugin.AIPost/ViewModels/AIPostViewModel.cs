using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.AIPost.Views;
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
using System.Runtime.Serialization;

namespace Plugin.AIPost.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum,
        CenterY,
        Scale,//像素当量
        SpaceBetween,//测量间距
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
        red = 0,
        blue = 1,
        green = 2,
        cyan = 3,
        yellow = 4,
        coral = 5,
        orange = 6,
        pink = 7,
    }
    #endregion

    [Category("深度学习")]
    [DisplayName("AI后处理")]
    [ModuleImageName("AIPost")]
    [Serializable]
    public class AIPostViewModel : ModuleBase
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

                // 修复1: 正确初始化 OutRegion
                if (OutRegion == null || !OutRegion.IsInitialized())
                {
                    OutRegion = new HRegion();
                    OutRegion.GenEmptyObj();
                   // Logger.AddLog("错误: AI返回结果无效", eMsgType.Info);
                }
                else
                {
                    OutRegion.GenEmptyObj();
                }

                HImage Result = ClassAi.AiRun(DispImage, ref erro, this.CONF_THRESHOLD, this.NMS_THRESHOLD);

                HRegion region1 = new HRegion();
                HRegion region2 = new HRegion();

                if (IsDispAiRegion)
                {
                    region1 = Result.Threshold(1.0, 1.0);
                    region2 = Result.Threshold(2.0, 2.0);

                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(region1), true));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai2", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(region2), true));
                }
                else
                {
                    region1 = Result.Threshold(1.0, 1.0);
                }

                // 修复：将region1设置为输出区域
                if (region1 != null && region1.IsInitialized())
                {
                    OutRegion = new HRegion(region1);
                }
                else
                {
                    OutRegion.GenEmptyObj();
                }

                // 像素当量 
                double.TryParse(GetLinkValue(Scale).ToString(), out double SS);

                ShowHRoi();

                // 调用算法 - 获取复制图像
                Algorithm.shuchutupian(Result, out HObject ho_DupImage);

                // 清理之前的复制图像
                if (DupImage != null && DupImage.IsInitialized())
                {
                    DupImage.Dispose();
                    DupImage = null;
                }

                // 关键修复：将算法输出的ho_DupImage转换为HImage并保存
                if (ho_DupImage != null && ho_DupImage.IsInitialized())
                {
                    // 将HObject转换为HImage
                    DupImage = new HImage(ho_DupImage);

                    // 获取图像大小和类型
                    if (DupImage != null && DupImage.IsInitialized())
                    {
                        try
                        {
                            HTuple width, height;
                            DupImage.GetImageSize(out width, out height);

                            // 获取图像类型
                            string imageType = DupImage.GetImageType();
                           // Logger.AddLog($"算法输出图像大小: {width.I} x {height.I}, 类型: {imageType}", eMsgType.Info);

                            // 获取完整图像区域
                            HRegion fullRegion = new HRegion();
                            fullRegion.GenRectangle1(0, 0, height - 1, width - 1);

                            // 检查图像灰度范围
                            HTuple min, max, range;
                            DupImage.MinMaxGray(fullRegion, 0, out min, out max, out range);
                           // Logger.AddLog($"算法输出图像灰度范围: 最小值={min.D:F2}, 最大值={max.D:F2}, 范围={range.D:F2}", eMsgType.Info);

                            // 清理区域
                            fullRegion.Dispose();

                            // 关键修复：如果灰度范围很小，进行对比度拉伸
                            if (max.D - min.D < 10)
                            {
                                //Logger.AddLog($"图像灰度范围较小，进行对比度拉伸以增强显示", eMsgType.Info);

                                try
                                {
                                    // 使用ScaleImageMax将图像灰度范围扩展到0-255
                                    HImage stretchedImage = DupImage.ScaleImageMax();
                                    DupImage.Dispose();
                                    DupImage = stretchedImage;

                                    // 再次检查灰度范围
                                    fullRegion = new HRegion();
                                    fullRegion.GenRectangle1(0, 0, height - 1, width - 1);
                                    DupImage.MinMaxGray(fullRegion, 0, out min, out max, out range);
                                   // Logger.AddLog($"对比度拉伸后图像灰度范围: 最小值={min.D:F2}, 最大值={max.D:F2}, 范围={range.D:F2}", eMsgType.Info);
                                    fullRegion.Dispose();
                                }
                                catch (Exception stretchEx)
                                {
                                    Logger.AddLog($"对比度拉伸失败: {stretchEx.Message}", eMsgType.Error);
                                }
                            }

                           
                        }
                        catch (Exception ex)
                        {
                            Logger.AddLog($"获取图像信息失败: {ex.Message}", eMsgType.Error);
                        }
                    }

                    // 显示算法输出的图像
                    //ShowImage(DupImage);

                    // 释放原始的HObject
                    ho_DupImage.Dispose();
                }
                else
                {
                    Logger.AddLog("算法输出的图像无效", eMsgType.Error);
                    // 如果算法返回无效图像，创建一个空图像
                    DupImage = new HImage();
                    DupImage.GenEmptyObj();
                }

                // 清理之前的AI输出图像
                if (OutputImage != null && OutputImage.IsInitialized())
                {
                    OutputImage.Dispose();
                    OutputImage = null;
                }

                // 保存AI处理后的图像
                if (Result != null && Result.IsInitialized())
                {
                    OutputImage = Result.CopyImage();

                    // 获取图像大小和类型
                    if (OutputImage != null && OutputImage.IsInitialized())
                    {
                        try
                        {
                            HTuple width, height;
                            OutputImage.GetImageSize(out width, out height);
                            string imageType = OutputImage.GetImageType();
                           // Logger.AddLog($"AI图像处理完成，输出图像大小: {width.I} x {height.I}, 类型: {imageType}", eMsgType.Info);

                            // 获取完整图像区域
                            HRegion fullRegion = new HRegion();
                            fullRegion.GenRectangle1(0, 0, height - 1, width - 1);

                            // 检查图像灰度范围
                            HTuple min, max, range;
                            OutputImage.MinMaxGray(fullRegion, 0, out min, out max, out range);
                           // Logger.AddLog($"AI输出图像灰度范围: 最小值={min.D:F2}, 最大值={max.D:F2}, 范围={range.D:F2}", eMsgType.Info);

                            // 清理区域
                            fullRegion.Dispose();

                            // 同样检查AI输出图像类型，如果不是字节图像，转换为适合显示的格式
                            if (imageType != "byte")
                            {
                               // Logger.AddLog($"将AI输出图像类型从 {imageType} 转换为字节图像", eMsgType.Info);

                                try
                                {
                                    HImage tempImage = OutputImage.ConvertImageType("byte");
                                    OutputImage.Dispose();
                                    OutputImage = tempImage;
                                }
                                catch (Exception convertEx)
                                {
                                    Logger.AddLog($"转换AI输出图像类型失败: {convertEx.Message}", eMsgType.Error);
                                }
                            }

                            // 关键修复：如果灰度范围很小，进行对比度拉伸
                            if (max.D - min.D < 10)
                            {
                              //  Logger.AddLog($"AI输出图像灰度范围较小，进行对比度拉伸以增强显示", eMsgType.Info);

                                try
                                {
                                    // 使用ScaleImageMax将图像灰度范围扩展到0-255
                                    HImage stretchedImage = OutputImage.ScaleImageMax();
                                    OutputImage.Dispose();
                                    OutputImage = stretchedImage;
                                }
                                catch (Exception stretchEx)
                                {
                                    Logger.AddLog($"AI输出图像对比度拉伸失败: {stretchEx.Message}", eMsgType.Error);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.AddLog($"获取AI输出图像信息失败: {ex.Message}", eMsgType.Error);
                        }
                    }
                }
                else
                {
                    Logger.AddLog("AI返回的图像无效", eMsgType.Error);
                    // 如果AI返回无效图像，创建一个空图像
                    OutputImage = new HImage();
                    OutputImage.GenEmptyObj();
                }

                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                if (OutRegion != null && OutRegion.IsInitialized())
                    OutRegion.Dispose();
                if (OutputImage != null && OutputImage.IsInitialized())
                    OutputImage.Dispose();
                if (DupImage != null && DupImage.IsInitialized())
                    DupImage.Dispose();
                return false;
            }
        }

        private void UpdateAiState()
        {
            string erro = "";
            if (ClassAi == null)
                ClassAi = new AI();

            if (IsOpen)
            {
                // 需要开启 AI：如果未运行则初始化
                if (!ClassAi.IsRunning)
                {
                    erro = ClassAi.AiInit(this.AiPath).ToString();
                    // 假设初始化成功时 erro 为空或 "0"（根据实际 AI 类调整判断条件）
                    DeviceStatic = string.IsNullOrEmpty(erro);
                    if (!DeviceStatic)
                    {
                        Logger.AddLog($"AI初始化失败: {erro}", eMsgType.Error);
                    }
                }
                else
                {
                    // 已经运行，无需操作
                    DeviceStatic = true;
                }
            }
            else
            {
                // 需要关闭 AI：如果正在运行则关闭
                if (ClassAi.IsRunning)
                {
                    ClassAi.AiClose(ref erro);
                    DeviceStatic = false;
                    if (!string.IsNullOrEmpty(erro))
                    {
                        Logger.AddLog($"AI关闭失败: {erro}", eMsgType.Error);
                    }
                }
                else
                {
                    DeviceStatic = false;
                }
            }
        }

        public override void InitModule()
        {
            base.Init();
            UpdateAiState();
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            // 修复: 确保 OutRegion 有效
            if (OutRegion == null || !OutRegion.IsInitialized())
            {
                OutRegion = new HRegion();
                OutRegion.GenEmptyObj();
            }
            // 输出 region1 区域
            AddOutputParam("区域", "HRegion", OutRegion);

            try
            {
              

                // 添加算法输出的图像参数
                if (DupImage == null || !DupImage.IsInitialized())
                {
                    DupImage = new HImage();
                    DupImage.GenEmptyObj();
                    Logger.AddLog("算法输出图像为空，创建空图像", eMsgType.Info);
                }
                else
                {
                    try
                    {
                        HTuple width, height;
                        DupImage.GetImageSize(out width, out height);
                        Logger.AddLog($"添加输出参数：算法输出图像，大小: {width.I} x {height.I}", eMsgType.Info);
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"获取算法输出图像尺寸失败: {ex.Message}", eMsgType.Error);
                    }
                }

                AddOutputParam("算法输出图像", "HImage", DupImage);
            }
            catch (Exception ex)
            {
                Logger.AddLog($"在添加输出参数时发生异常：{ex.Message}", eMsgType.Error);
            }
        }

        // 修改：移除override，改为普通的公共方法
        public new void Dispose()
        {
            // 清理资源
            if (OutputImage != null && OutputImage.IsInitialized())
            {
                OutputImage.Dispose();
                OutputImage = null;
            }

            if (OutRegion != null && OutRegion.IsInitialized())
            {
                OutRegion.Dispose();
                OutRegion = null;
            }

            if (DupImage != null && DupImage.IsInitialized())
            {
                DupImage.Dispose();
                DupImage = null;
            }

            if (ClassAi != null)
            {
                string erro = "";
                ClassAi.AiClose(ref erro);
                ClassAi = null;
            }
        }

        #region Prop
        [NonSerialized]
        int ChannelNum = 1;
        [NonSerialized]
        private HRegion _ReduceRegion;
        [NonSerialized]
        private HImage _OutputImage;
        [NonSerialized]
        private HImage _DupImage; // 新增：用于存储算法输出的图像
        private bool _DeviceStatic = false;
        private string _AiPath = "";
        private double cONF_THRESHOLD = 0.5, nMS_THRESHOLD = 0.5;
        private int _DefectNum = 4;
        private eLocationClass _SelectLocation = eLocationClass.Center;

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
            get { return cONF_THRESHOLD; }
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
            set
            {
                if (_IsOpen != value)
                {
                    _IsOpen = value;
                    RaisePropertyChanged();
                    UpdateAiState(); // 根据新值更新 AI 状态
                }
            }
        }

        //新增判断显示
        private bool _IsFindRegions = false;
        public bool IsFindRegions
        {
            get { return _IsFindRegions; }
            set { _IsFindRegions = value; RaisePropertyChanged(); }
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

        //初始化，像素当量
        private LinkVarModel _Scale = new LinkVarModel() { Value = 1.0, Text = "1" };
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

        public HImage OutputImage
        {
            get
            {
                if (_OutputImage == null || !_OutputImage.IsInitialized())
                {
                    _OutputImage = new HImage();
                    _OutputImage.GenEmptyObj();
                }
                return _OutputImage;
            }
            set { Set(ref _OutputImage, value); }
        }

        // 新增属性：算法输出图像
        public HImage DupImage
        {
            get
            {
                if (_DupImage == null || !_DupImage.IsInitialized())
                {
                    _DupImage = new HImage();
                    _DupImage.GenEmptyObj();
                }
                return _DupImage;
            }
            set { Set(ref _DupImage, value); }
        }
        #endregion

        #region //后处理参数
        private bool useScore = false, useID = false, useLimtHeight = false, useLimtWidth = false;
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
        private double limtHeight = 9999, limtWidth = 9999;
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
            var view = ModuleView as AIPostView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (InputImageLinkText == null || InputImageLinkText == "")
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                view.mWindowH.hControl.MouseUp += HControl_MouseUp;

                GetDispImage(InputImageLinkText);
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
                        var view = ModuleView as AIPostView;
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
            string[] parts = obj.SendName.Split(',');
            if (parts.Length < 2) return;

            string command = parts[1];

            switch (command)
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    Logger.AddLog($"InputImageLink 更新: {obj.LinkName}", eMsgType.Info);
                    if (InputImageLinkText == null) return;
                    GetDispImage(InputImageLinkText);
                    break;

                case "Scale":
                    Scale.Text = obj.LinkName;
                    Logger.AddLog($"Scale 更新: {obj.LinkName}", eMsgType.Info);
                    break;

                default:
                    Logger.AddLog($"未知链接命令: {command}", eMsgType.Info);
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
                        var view = ModuleView as AIPostView;
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
                var view = ModuleView as AIPostView;
                if (view == null) return; ;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                view.mWindowH.DispObj(finalRegion);
                if (index.Length < 1) return;
                RoiList[index] = roi;
            }
            catch (Exception ex)
            {
                Logger.AddLog($"HControl_MouseUp异常: {ex.Message}", eMsgType.Error);
            }
        }

        [NonSerialized]
        VMHWindowControl mWindowH;
        private void ShowImage(HImage image)
        {
            var view = ModuleView as AIPostView;
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
            bool dispDrawRoi = true;

            var view = ModuleView as AIPostView;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispDrawRoi = false;
            }
            else
            {
                mWindowH = view.mWindowH;
            }

            if (ReduceRegion != null && ReduceRegion.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(ReduceRegion)));
            }
            if (OutRegion != null && OutRegion.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", new HObject(OutRegion), true));
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
        }
        #endregion
    }
}