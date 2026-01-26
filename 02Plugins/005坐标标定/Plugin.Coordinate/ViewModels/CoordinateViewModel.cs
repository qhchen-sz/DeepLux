using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Plugin.Coordinate.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Linq;
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
using HV.Views;
using HV.Views.Dock;

namespace Plugin.Coordinate.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        XLink,
        YLink,
        DegLink,
    }
    #endregion

    [Category("坐标标定")]
    [DisplayName("坐标补正")]
    [ModuleImageName("Coordinate")]
    [Serializable]
    public class CoordinateViewModel : ModuleBase
    {
        enum ComponentsType {模板匹配=0, 线线构建=1 };

        private ComponentsType PhiType = ComponentsType.模板匹配; 
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

            int index = Prj.ModuleList.IndexOf(this);
        
            for (int i = index - 1; i >= 0; i--)
            {
                /*extractedText*/
                if (Prj.ModuleList[i].ModuleParam.ModuleName.Contains("模板匹配"))
                {
                    PhiType = ComponentsType.模板匹配;
                    HomMat2D = Prj.ModuleList[i].HomMat2D;
                    XLinkText.Text = $"&{Prj.ModuleList[i].ModuleParam.ModuleName}.X";
                    YLinkText.Text = $"&{Prj.ModuleList[i].ModuleParam.ModuleName}.Y";
                    DegLinkText.Text = $"&{Prj.ModuleList[i].ModuleParam.ModuleName}.Deg";
                    return;
                }
                if (Prj.ModuleList[i].ModuleParam.ModuleName.Contains("线线构建"))
                {
                    PhiType = ComponentsType.线线构建;
                    HomMat2D = Prj.ModuleList[i].HomMat2D;
                    XLinkText.Text = $"&{Prj.ModuleList[i].ModuleParam.ModuleName}.交点X";
                    YLinkText.Text = $"&{Prj.ModuleList[i].ModuleParam.ModuleName}.交点Y";
                    DegLinkText.Text = $"&{Prj.ModuleList[i].ModuleParam.ModuleName}.角度";
                    return;
                }
            }
        }
        
        public override bool ExeModule()
        {
            if (ModuleParam.ModuleName.StartsWith ("坐标补正结束"))
            {
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            Stopwatch.Restart();
            try
            {
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetModeCoord();
                MathCoord.X = double.Parse(GetLinkValue(XLinkText).ToString());
                MathCoord.Y = double.Parse(GetLinkValue(YLinkText).ToString());
                if (PhiType == ComponentsType.模板匹配)
                {
                    MathCoord.Phi = double.Parse(GetLinkValue(DegLinkText).ToString());
                }
                else
                {
                    MathCoord.Phi = double.Parse(GetLinkValue(DegLinkText).ToString()) / 180 * Math.PI;
                }
                HOperatorSet.VectorAngleToRigid(ModeCoord.Y, ModeCoord.X, ModeCoord.Phi, MathCoord.Y, MathCoord.X, MathCoord.Phi, out HomMat2D);
                HOperatorSet.VectorAngleToRigid(MathCoord.Y, MathCoord.X, MathCoord.Phi, ModeCoord.Y, ModeCoord.X, ModeCoord.Phi, out HomMat2D_Inverse);
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
        #region Prop
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
                    ExeModule();
                }
            }
        }
        private LinkVarModel _XLinkText = new LinkVarModel() { Value = 0 };
        /// <summary>
        /// X链接文本
        /// </summary>
        public LinkVarModel XLinkText
        {
            get { return _XLinkText; }
            set { Set(ref _XLinkText, value); }
        }
        private LinkVarModel _YLinkText = new LinkVarModel() { Value = 0 };
        /// <summary>
        /// Y链接文本
        /// </summary>
        public LinkVarModel YLinkText
        {
            get { return _YLinkText; }
            set { Set(ref _YLinkText, value); }
        }
        private LinkVarModel _DegLinkText = new LinkVarModel() { Value = 0 };
        /// <summary>
        /// Deg链接文本
        /// </summary>
        public LinkVarModel DegLinkText
        {
            get { return _DegLinkText; }
            set { Set(ref _DegLinkText, value); }
        }
        private bool _ShowCoordinate=true;
        /// <summary>
        /// 显示坐标轴
        /// </summary>
        public bool ShowCoordinate
        {
            get { return _ShowCoordinate; }
            set { Set(ref _ShowCoordinate, value); }
        }

        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as CoordinateView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (DispImage == null )
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                GetDispImage(InputImageLinkText,true);
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
                        var view = this.ModuleView as CoordinateView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "XLink":
                    XLinkText.Text = obj.LinkName;
                    break;
                case "YLink":
                    YLinkText.Text = obj.LinkName;
                    break;
                case "DegLink":
                    DegLinkText.Text = obj.LinkName;
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
                            case eLinkCommand.XLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},XLink");
                                break;
                            case eLinkCommand.YLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},YLink");
                                break;
                            case eLinkCommand.DegLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},DegLink");
                                break;
                            default:
                                break;
                        }

                    });
                }
                return _LinkCommand;
            }
        }
        #endregion

        #region Method
        /// <summary>
        /// 获取模板坐标
        /// </summary>
        private void GetModeCoord()
        {
            int index = Prj.ModuleList.IndexOf(this);
            string extractedText = null;
            if (!string.IsNullOrEmpty(XLinkText.Text))
            {
                string input = XLinkText.Text.ToString();
                int startIndex = input.IndexOf('&') + 1;
                int endIndex = input.IndexOf('.');
                if(endIndex>0)
                    extractedText = input.Substring(startIndex, endIndex - startIndex);
                else
                    extractedText = XLinkText.Text.ToString();


            }
            for (int i = index-1; i >= 0; i--)
            {
                /*extractedText*/
                if (!string.IsNullOrEmpty(extractedText)&&Prj.ModuleList[i].ModuleParam.ModuleName.StartsWith(extractedText))
                {
                    ModeCoord = Prj.ModuleList[i].ModeCoord; return;
                }
               

            }
        }
        #endregion

    }

}
