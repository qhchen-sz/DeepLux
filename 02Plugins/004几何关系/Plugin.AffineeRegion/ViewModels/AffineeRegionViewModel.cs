using EventMgrLib;
using HalconDotNet;
using Plugin.AffineeRegion.Views;
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

namespace Plugin.AffineeRegion.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        InputRegionLink,
        Rect1X1,
        Rect1Y1,
        Rect1X2,
        Rect1Y2,
        Rect1Angle1,
        Rect1Angle2
    }

    /// <summary>
    /// 插值算法枚举
    /// </summary>
    public enum InterpolationMethod
    {
        nearest_neighbor,
        constant
    }

    #endregion

    [Category("几何关系")]
    [DisplayName("仿射区域")]
    [ModuleImageName("AffineeRegion")]
    [Serializable]
    public class AffineeRegionViewModel : ModuleBase
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
                if (!IsManMual)
                    GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取输入区域
                HRegion inputRegion = new HRegion();
                if (!string.IsNullOrEmpty(InputRegionLinkText))
                {
                    object regionObj = GetLinkValue(InputRegionLinkText);
                    if (regionObj is HRegion)
                    {
                        inputRegion = (HRegion)regionObj;
                    }
                }

                if (inputRegion == null || !inputRegion.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取仿射参数
                double srcRow = Convert.ToDouble(GetLinkValue(Rect1Y1));       // 起点Y（输入图像左上角Y）
                double srcCol = Convert.ToDouble(GetLinkValue(Rect1X1));       // 起点X（输入图像左上角X）
                double srcAngle = Convert.ToDouble(GetLinkValue(Rect1Angle1));  // 起点角度（输入图像旋转角度）

                double dstRow = Convert.ToDouble(GetLinkValue(Rect1Y2));        // 终点Y（输入区域左上角Y）
                double dstCol = Convert.ToDouble(GetLinkValue(Rect1X2));        // 终点X（输入区域左上角X）
                double dstAngle = Convert.ToDouble(GetLinkValue(Rect1Angle2));  // 终点角度（输入区域旋转角度）

                // 将角度转换为弧度
                double srcAngleRad = srcAngle * Math.PI / 180.0;
                double dstAngleRad = dstAngle * Math.PI / 180.0;

                // 计算旋转角度差
                double angleDiff = dstAngleRad - srcAngleRad;

                // 创建仿射变换矩阵：从输入区域坐标系变换到输入图像坐标系
                // 1. 平移到原点 -> 旋转 -> 平移到目标位置
                HTuple homMat2D;
                HOperatorSet.VectorAngleToRigid(0, 0, 0, dstRow - srcRow, dstCol - srcCol, angleDiff, out homMat2D);

                // 获取插值算法
                string interpolationType = SelectedInterpolationMethod.ToString().ToLower();

                // 应用仿射变换到输入区域
                OutRegion = inputRegion.AffineTransRegion(new HHomMat2D(homMat2D), interpolationType);

                // 显示变换后的区域（使用ROI形式）
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                    HRoiType.检测结果, "green", new HObject(OutRegion)));

                //// 显示输入区域（参考用，蓝色）
                //ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                //    HRoiType.检测范围, "blue", new HObject(inputRegion)));

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
            AddOutputParam("区域", "HRegion", OutRegion);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop
        private bool IsManMual = true;
        HRegion OutRegion = new HRegion(0.0, 0, 3);

        /// <summary>
        /// 插值算法选项列表
        /// </summary>
        public InterpolationMethod[] InterpolationMethods { get; } = (InterpolationMethod[])Enum.GetValues(typeof(InterpolationMethod));

        private InterpolationMethod _SelectedInterpolationMethod = InterpolationMethod.nearest_neighbor;
        /// <summary>
        /// 选择的插值算法
        /// </summary>
        public InterpolationMethod SelectedInterpolationMethod
        {
            get { return _SelectedInterpolationMethod; }
            set { Set(ref _SelectedInterpolationMethod, value); }
        }
        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        private LinkVarModel _Rect1X1 = new LinkVarModel() { Text = "0" };
        public LinkVarModel Rect1X1
        {
            get { return _Rect1X1; }
            set { _Rect1X1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1Y1 = new LinkVarModel() { Text = "0" };
        public LinkVarModel Rect1Y1
        {
            get { return _Rect1Y1; }
            set { _Rect1Y1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1X2 = new LinkVarModel() { Text = "0" };
        public LinkVarModel Rect1X2
        {
            get { return _Rect1X2; }
            set { _Rect1X2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1Y2 = new LinkVarModel() { Text = "0" };
        public LinkVarModel Rect1Y2
        {
            get { return _Rect1Y2; }
            set { _Rect1Y2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1Angle1 = new LinkVarModel() { Text = "0" };
        /// <summary>
        /// 起点角度
        /// </summary>
        public LinkVarModel Rect1Angle1
        {
            get { return _Rect1Angle1; }
            set { _Rect1Angle1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Rect1Angle2 = new LinkVarModel() { Text = "0" };
        /// <summary>
        /// 终点角度
        /// </summary>
        public LinkVarModel Rect1Angle2
        {
            get { return _Rect1Angle2; }
            set { _Rect1Angle2 = value; RaisePropertyChanged(); }
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
                //if (DispImage != null && DispImage.IsInitialized())
                //{
                //    ShowHRoi();
                //    ShowInputRegion();
                //}
            }
        }
        private string _InputRegionLinkText;
        /// <summary>
        /// 输入区域链接文本
        /// </summary>
        public string InputRegionLinkText
        {
            get { return _InputRegionLinkText; }
            set
            {
                _InputRegionLinkText = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as AffineeRegionView;
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
                case eLinkCommand.InputRegionLink:
                    InputRegionLinkText = obj.LinkName;
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
                case eLinkCommand.Rect1Angle1:
                    Rect1Angle1.Text = obj.LinkName;
                    break;
                case eLinkCommand.Rect1Angle2:
                    Rect1Angle2.Text = obj.LinkName;
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
                            case eLinkCommand.InputRegionLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRegionLink");
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
                            case eLinkCommand.Rect1Angle1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Angle1");
                                break;
                            case eLinkCommand.Rect1Angle2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Angle2");
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
                        var view = this.ModuleView as AffineeRegionView;
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
        public void ShowInputRegion()
        {
            var view = ModuleView as AffineeRegionView;
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

            // 获取输入区域并显示
            if (!string.IsNullOrEmpty(InputRegionLinkText))
            {
                object regionObj = GetLinkValue(InputRegionLinkText);
                if (regionObj is HRegion inputRegion && inputRegion.IsInitialized())
                {
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                        HRoiType.检测范围, "blue", new HObject(inputRegion)));
                }
            }
        }
        #endregion
    }
}
