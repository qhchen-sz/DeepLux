using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Plugin.Parallel.Views;
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

namespace Plugin.Parallel.ViewModels
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

    [Category("逻辑工具")]
    [DisplayName("并行处理")]
    [ModuleImageName("Parallel")]
    [Serializable]
    public class ParallelViewModel : ModuleBase
    {

        public override void SetDefaultLink()
        {
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                Project project = Solution.Ins.GetProjectById(ModuleParam.ProjectID);
                List<string> Select = CheckMode(project);

                // 创建任务列表
                List<Task> tasks = new List<Task>();
                foreach (var item in Select)
                {

                        // 捕获当前值避免闭包问题
                 if(item == "延时工具")
                    {
                        project.ModuleDic[item].ExeModule();
                        project.UpsetUI(project.ModuleDic[item].ModuleParam);
                    }
                    else
                    {
                        string currentItem = item;
                        tasks.Add(Task.Run(() =>
                        {
                            project.ModuleDic[currentItem].ExeModule();
                            project.UpsetUI(project.ModuleDic[currentItem].ModuleParam);
                        }));
                    }

                        
                    
                }

                // 等待所有任务完成
                Task.WaitAll(tasks.ToArray());

                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Logger.GetExceptionMsg(ex);
                }
                ChangeModuleRunStatus(eRunStatus.NG);
                return false;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
            finally
            {
                Stopwatch.Stop();
                Logger.AddLog($"模块执行耗时: {Stopwatch.ElapsedMilliseconds}ms",eMsgType.Info);
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
        #region 方法
        private List<string> CheckMode(Project project)
        {
            List<string> ee = project.ModuleTreeNodeMap[ModuleParam.ModuleName].Parent.ChildList;
            List<string> SelectModel = new List<string>();
            bool start = false;
            bool luoji = false;
            string startmodename = "";
            foreach (var item in ee)
            {
                if (item.StartsWith("并行处理开始") && !start)
                {
                    start = true;
                    startmodename = (item.Remove(4, 2)).Insert(4,"结束");
                } 
                else if (item== startmodename)
                    start = false;
                else
                {

                    if(item.StartsWith("并行处理开始")|| item.StartsWith("循环开始") || item.StartsWith("坐标补正开始") || item.StartsWith("如果") || item.StartsWith("点云补正开始"))
                    {
                        luoji = true;
                    }
                    if (luoji)
                    {
                        if (item.StartsWith("并行处理结束") || item.StartsWith("循环结束") || item.StartsWith("坐标补正结束") || item.StartsWith("结束") || item.StartsWith("点云补正结束"))
                        {
                            luoji = false;
                            continue;
                        }
                    }

                    if (!luoji && start)
                    {
                        SelectModel.Add(item);
                    }
                }
            }
            return SelectModel;
        }
        #endregion

    }

}
