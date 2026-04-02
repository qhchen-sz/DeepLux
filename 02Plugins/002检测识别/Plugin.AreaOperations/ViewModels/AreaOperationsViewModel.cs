using EventMgrLib;
using HalconDotNet;
using Plugin.AreaOperations.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
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

namespace Plugin.AreaOperations.ViewModels
{
    #region enum
    /// <summary>
    /// 链接命令枚举
    /// </summary>
    public enum eLinkCommand
    {
        InputImageLink,
        InputRegion1Link,
        InputRegion2Link
    }

    /// <summary>
    /// 区域运算类型枚举
    /// </summary>
    public enum OperationType
    {
        合并,      // Union
        相交,      // Intersection
        相减,      // Difference
        相加,      // Addition
        对称相减   // Symmetric Difference
    }

    #endregion

    [Category("检测识别")]
    [DisplayName("区域运算")]
    [ModuleImageName("AreaOperations")]
    [Serializable]
    public class AreaOperationsViewModel : ModuleBase
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

                // 检查输入图像
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                //if (!IsManMual)
                GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取输入区域1
                HRegion inputRegion1 = new HRegion();
                if (!string.IsNullOrEmpty(InputRegion1LinkText))
                {
                    object regionObj = GetLinkValue(InputRegion1LinkText);
                    if (regionObj is HRegion)
                    {
                        inputRegion1 = (HRegion)regionObj;
                    }
                }

                if (inputRegion1 == null || !inputRegion1.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取输入区域2
                HRegion inputRegion2 = new HRegion();
                if (!string.IsNullOrEmpty(InputRegion2LinkText))
                {
                    object regionObj = GetLinkValue(InputRegion2LinkText);
                    if (regionObj is HRegion)
                    {
                        inputRegion2 = (HRegion)regionObj;
                    }
                }

                if (inputRegion2 == null || !inputRegion2.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 根据运算类型执行相应操作
                switch (SelectedOperationType)
                {
                    case OperationType.合并:
                        // 合并：两个区域合并成一个（单一轮廓）
                        OutRegion = inputRegion1.Union2(inputRegion2);
                        break;
                    case OperationType.相交:
                        // 相交：取两个区域的交集
                        OutRegion = inputRegion1.Intersection(inputRegion2);
                        break;
                    case OperationType.相减:
                        // 相减：从区域1中减去区域2
                        OutRegion = inputRegion1.Difference(inputRegion2);
                        break;
                    case OperationType.相加:
                        // 相加：两个区域各自保留轮廓，拼接在一起
                        HOperatorSet.ConcatObj(inputRegion1, inputRegion2, out HObject concatObj);
                        OutRegion = new HRegion(concatObj);
                        break;
                    case OperationType.对称相减:
                        // 对称相减：合并减去交集（即两个区域不重叠的部分）
                        HRegion union = inputRegion1.Union2(inputRegion2);
                        HRegion intersection = inputRegion1.Intersection(inputRegion2);
                        OutRegion = union.Difference(intersection);
                        break;
                    default:
                        OutRegion = inputRegion1.Union2(inputRegion2);
                        break;
                }

                //// 根据显示设置显示区域,不设置
                //if (ShowRegion)
                //{
                //    // 显示输入区域1（蓝色）
                //    if (inputRegion1 != null && inputRegion1.IsInitialized())
                //    {
                //        //mWindowH.WindowH.DispHobject(inputRegion1, "blue", FillDisplay);
                //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                //                HRoiType.检测范围, "blue", new HObject(inputRegion1), FillDisplay));
                //    }

                //    // 显示输入区域2（红色）
                //    if (inputRegion2 != null && inputRegion2.IsInitialized())
                //    {
                //        //mWindowH.WindowH.DispHobject(inputRegion2, "red", FillDisplay);
                //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                //                HRoiType.检测范围, "yellow", new HObject(inputRegion2), FillDisplay));
                //    }
                //}

                // 显示运算结果区域（使用ROI形式）
                if (OutRegion != null && OutRegion.IsInitialized())
                {
                    //mWindowH.WindowH.DispHobject(OutRegion, "green", FillDisplay);
                    //如果为相加，两个区域单独显示
                    if (SelectedOperationType == OperationType.相加)
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "region1", ModuleParam.Remarks,
                            HRoiType.检测结果, "blue", new HObject(inputRegion1), FillDisplay));
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "region2", ModuleParam.Remarks,
                            HRoiType.检测结果, "yellow", new HObject(inputRegion2), FillDisplay));
                    }
                    else {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                                HRoiType.检测结果, "green", new HObject(OutRegion), FillDisplay));
                    }

                }
                ShowHRoi();
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
            AddOutputParam("区域", "HRegion", OutRegion);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
            // 如果启用输出区域图像，则生成并输出
            if (OutputRegionImage)
            {
                try
                {
                    // 检查 OutRegion 有效性：非 null、已初始化、面积 > 0
                    if (OutRegion != null && OutRegion.IsInitialized() && OutRegion.Area > 0)
                    {
                        // 获取全图区域
                        HRegion fullRegion = DispImage.GetDomain();
                        // 计算 ROI 区域的补集（全图区域减去 ROI 区域）
                        HRegion complementRegion = fullRegion.Difference(OutRegion);
                        if (complementRegion != null && complementRegion.IsInitialized() && complementRegion.Area > 0)
                        {
                            //补集全涂黑，其他保持不变
                            OutImage = new RImage(complementRegion.PaintRegion(DispImage, 0d, "fill"));
                            if (OutImage != null && OutImage.IsInitialized())
                            {
                                AddOutputParam("区域图像", "HImage", OutImage);
                            }
                            else
                            {
                                AddOutputParam("区域图像", "HImage", new RImage());
                            }
                            complementRegion.Dispose();
                        }
                        else
                        {
                            AddOutputParam("区域图像", "HImage", new RImage());
                        }
                        fullRegion.Dispose();
                    }
                    else
                    {
                        // 若无效，添加默认空图像
                        AddOutputParam("区域图像", "HImage", new RImage());
                    }
                }
                catch (Exception ex)
                {
                    Logger.GetExceptionMsg(ex);
                    AddOutputParam("区域图像", "HImage", new RImage());
                }
                //OutImage = new RImage(DispImage.ReduceDomain(OutRegion));
            }
            //if (OutputRegionImage && OutImage != null && OutImage.IsInitialized())
            //{
            //    AddOutputParam("区域图像", "HImage", OutImage);
            //}
        }

        #region Prop
        //private bool IsManMual = true;
        [NonSerialized]
        private HRegion _OutRegion;
        /// <summary>
        /// 输出区域
        /// </summary>
        public HRegion OutRegion
        {
            get { return _OutRegion ?? (_OutRegion = new HRegion()); }
            set { _OutRegion = value; }
        }
        [NonSerialized]
        private RImage _OutImage;
        /// <summary>
        /// 输出图像
        /// </summary>
        public RImage OutImage
        {
            get { return _OutImage; }
            set { _OutImage = value; }
        }

        /// <summary>
        /// 运算类型选项列表
        /// </summary>
        public OperationType[] OperationTypes { get; } = (OperationType[])Enum.GetValues(typeof(OperationType));

        private OperationType _SelectedOperationType = OperationType.合并;
        /// <summary>
        /// 选择的运算类型
        /// </summary>
        public OperationType SelectedOperationType
        {
            get { return _SelectedOperationType; }
            set { Set(ref _SelectedOperationType, value); }
        }

/*        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();*/

        #region 显示设置属性
        private bool _ShowRegion = false;
        /// <summary>
        /// 是否显示区域
        /// </summary>
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        private bool _FillDisplay = false;
        /// <summary>
        /// 是否填充显示
        /// </summary>
        public bool FillDisplay
        {
            get { return _FillDisplay; }
            set { Set(ref _FillDisplay, value); }
        }

        private bool _OutputRegionImage = false;
        /// <summary>
        /// 是否输出区域图像
        /// </summary>
        public bool OutputRegionImage
        {
            get { return _OutputRegionImage; }
            set { Set(ref _OutputRegionImage, value); }
        }
        #endregion

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
            }
        }

        private string _InputRegion1LinkText;
        /// <summary>
        /// 输入区域1链接文本
        /// </summary>
        public string InputRegion1LinkText
        {
            get { return _InputRegion1LinkText; }
            set
            {
                _InputRegion1LinkText = value;
                RaisePropertyChanged();
            }
        }

        private string _InputRegion2LinkText;
        /// <summary>
        /// 输入区域2链接文本
        /// </summary>
        public string InputRegion2LinkText
        {
            get { return _InputRegion2LinkText; }
            set
            {
                _InputRegion2LinkText = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as AreaOperationsView;
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
                case eLinkCommand.InputRegion1Link:
                    InputRegion1LinkText = obj.LinkName;
                    break;
                case eLinkCommand.InputRegion2Link:
                    InputRegion2LinkText = obj.LinkName;
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
                            case eLinkCommand.InputRegion1Link:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRegion1Link");
                                break;
                            case eLinkCommand.InputRegion2Link:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRegion2Link");
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
                        //IsManMual = true;
                        ExeModule();
                        //IsManMual = false;
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
                        var view = this.ModuleView as AreaOperationsView;
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

    }
}
