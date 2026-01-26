using EventMgrLib;
using HalconDotNet;
using Plugin.PLCWrite.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
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
using HV.Communacation;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using System.Collections.ObjectModel;
using HV.Script;

namespace Plugin.PLCWrite.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Line1,
        Line2,
    }
    #endregion

    [Category("文件通讯")]
    [DisplayName("PLC写入")]
    [ModuleImageName("PLCWrite")]
    [Serializable]
    public class PLCWriteViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                string plcKey = "";
                //foreach (var item in EComManageer.s_ECommunacationDic)
                //{
                //    if (item.Value.m_connectKey == CurKey)
                //    {
                //        plcKey = item.Key;
                //    }
                //}
                bool ExeOk = false;
                foreach (var item in PlcWriteVar)
                {

                    VarModel var = item as VarModel;
                    //if (expressionView == null)
                    //{
                    //    expressionView = new ExpressionView();
                    //    expressionView.Var = var;
                    //}

                    var.m_TempScriptSupport.Source = ExpressionScriptTemplate.GetScriptCode(
                        ModuleParam.ProjectID,
                        ModuleParam.ModuleName,
                        var.Expression);
                    if (var.IsCompileSuccess || var.m_TempScriptSupport.Compile())
                    {
                        var.IsCompileSuccess = true;
                        var codeRunValue = var.m_TempScriptSupport.CodeRun();
                        switch (var.DataType)
                        {
                            case "int":
                                if (!(codeRunValue is int))
                                {
                                    codeRunValue = 0;
                                }

                                break;
                            case "double":
                                if (!(codeRunValue is double || codeRunValue is int))
                                {
                                    codeRunValue = 0;
                                }

                                break;
                            case "string":
                                break;
                            case "bool":
                                if (!(codeRunValue is bool))
                                {
                                    codeRunValue = false;
                                }

                                break;
                            default:
                                break;

                                
                                //var.Expression = expressionView.viewModel.MyEditer.Text;
                        }
                        item.Value = codeRunValue;
                    }
                    

                    if (!EComManageer.writeRegister(CurKey, item.DataTypePlc, item.NamePlc, item.Value.ToString()))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    ChangeModuleRunStatus(eRunStatus.OK);

                }

                //if (plcKey != "")
                //{
                //    string value = GetLinkValue(LinkValue).ToString();
                    
                //    for (int i = 0; i < Repetitions; i++)
                //    {
                //        ExeOk = EComManageer.writeRegister(plcKey, DataType, StartAddress, value);
                //        if (ExeOk) { break; }
                //        Thread.Sleep(10);  
                //    }
                //    if (ExeOk)
                //    {
                //        Logger.AddLog(value);
                //        ChangeModuleRunStatus(eRunStatus.OK);
                //    }
                //    else
                //    {
                //        ChangeModuleRunStatus(eRunStatus.NG);
                //    }
                //}
                //else
                //{
                //    ChangeModuleRunStatus(eRunStatus.NG);
                //}
                //if (Continue)
                //{
                //    Prj.ExeModuleName = "";
                //}
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
            //switch (DataType)
            //{
            //    case PLCDataWriteReadTypeEnum.布尔:
            //        break;
            //    case PLCDataWriteReadTypeEnum.整型:
            //        AddOutputParam("读取int值", "int", ReadValue);
            //        break;
            //    case PLCDataWriteReadTypeEnum.浮点:
            //        AddOutputParam("读取double值", "double", ReadValue);
            //        break;
            //    case PLCDataWriteReadTypeEnum.字符串:
            //        break;
            //}
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop
        private ObservableCollection<WriteVarModel> _PlcWriteVar = new ObservableCollection<WriteVarModel>();
        /// <summary>
        /// 变量
        /// </summary>
        public ObservableCollection<WriteVarModel> PlcWriteVar
        {
            get { return _PlcWriteVar; }
            set { _PlcWriteVar = value; RaisePropertyChanged(); }
        }
        private WriteVarModel _SelectPlcWriteVar;
        public WriteVarModel SelectPlcWriteVar
        {
            get { return _SelectPlcWriteVar; }
            set { _SelectPlcWriteVar = value; RaisePropertyChanged(); }
        }
        private int _Repetitions=1;
        /// <summary>
        /// 模块执行错误 重复写次数
        /// </summary>
        public int Repetitions
        {
            get { return _Repetitions; }
            set { _Repetitions = value; RaisePropertyChanged(); }
        }
        private bool _Continue;
        public bool Continue
        {
            get { return _Continue; }
            set { _Continue = value; }  
        }
        /// <summary>
        /// 寄存器起始地址
        /// </summary>
        private int _StartAddress = 1;
        public int StartAddress
        {
            get { return _StartAddress; }
            set { Set(ref _StartAddress, value); }
        }
        /// <summary>
        /// 备注
        /// </summary>
        private string _Remarks;
        public string Remarks
        {
            get { return _Remarks; }
            set { Set(ref _Remarks, value); }
        }
        /// <summary>
        /// Com口数据,用来显示KEY列表数据源
        /// </summary>
        private List<string> _ComKeys;
        public List<string> ComKeys
        {
            get
            {
                if (_ComKeys == null) _ComKeys = new List<string>();
                return _ComKeys;
            }
            set { _ComKeys = value; RaisePropertyChanged(); }
        }
        /// <summary>
        /// 当前Key
        /// </summary>
        private string _CurKey = "";
        public string CurKey
        {
            get { return _CurKey; }
            set { _CurKey = value; RaisePropertyChanged(); }
        }
        private PLCDataWriteReadTypeEnum _DataType = PLCDataWriteReadTypeEnum.浮点;
        /// <summary>
        /// 指定图像
        /// </summary>
        public PLCDataWriteReadTypeEnum DataType
        {
            get
            {
                return _DataType;
            }
            set
            {
                Set(ref _DataType, value);
            }
        }

        private LinkVarModel _LinkValue = new LinkVarModel() { Value = 10 };
        /// <summary>
        /// 延时时间
        /// </summary>
        public LinkVarModel LinkValue
        {
            get { return _LinkValue; }
            set { _LinkValue = value; RaisePropertyChanged(); }
        }
        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            ComKeys = EComManageer.GetPLCConnectKeys();
            upDataRemaks();
            if (CurKey == ""&& ComKeys != null && ComKeys.Count > 0)
            {
                CurKey = ComKeys[0];
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
                        switch (DataType)
                        {
                            case PLCDataWriteReadTypeEnum.布尔:
                                break;
                            case PLCDataWriteReadTypeEnum.整型:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                break;
                            case PLCDataWriteReadTypeEnum.浮点:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                break;
                            case PLCDataWriteReadTypeEnum.字符串:
                                break;

                        }                        
                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LinkText");
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
                        var view = this.ModuleView as PLCWriteView;
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
        private CommandBase _AddVarCommand;
        public CommandBase AddVarCommand
        {
            get
            {
                if (_AddVarCommand == null)
                {
                    _AddVarCommand = new CommandBase((obj) =>
                    {
                        switch (obj)
                        {
                            case "int":
                                PlcWriteVar.Add(new WriteVarModel() { DataTypePlc = PLCDataWriteReadTypeEnum.整型, NamePlc = "Value"+ PlcWriteVar.Count, NotePlc = "" ,DataType = "int"});
                                break;
                            case "double":
                                PlcWriteVar.Add(new WriteVarModel() { DataTypePlc = PLCDataWriteReadTypeEnum.浮点, NamePlc = "Value" + PlcWriteVar.Count, NotePlc = "" ,DataType = "double" });
                                break;
                            case "string":
                                PlcWriteVar.Add(new WriteVarModel() { DataTypePlc = PLCDataWriteReadTypeEnum.字符串, NamePlc = "Value" + PlcWriteVar.Count, NotePlc = "" ,DataType = "string" });
                                break;
                            case "bool":
                                PlcWriteVar.Add(new WriteVarModel() { DataTypePlc = PLCDataWriteReadTypeEnum.布尔, NamePlc = "Value" + PlcWriteVar.Count, NotePlc = "" ,DataType = "bool" });
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _AddVarCommand;
            }
        }
        [NonSerialized]
        private CommandBase _DeleteCommand;
        public CommandBase DeleteCommand
        {
            get
            {
                if (_DeleteCommand == null)
                {
                    _DeleteCommand = new CommandBase((obj) =>
                    {
                        PlcWriteVar.Remove(SelectPlcWriteVar);
                        //var view = ModuleView as PLCReadView;
                        //if (view.dg.SelectedIndex == -1) return;
                        //Vars.RemoveAt(view.dg.SelectedIndex);
                        //UpdateData();
                    });
                }
                return _DeleteCommand;
            }
        }
        [NonSerialized]
        private CommandBase _MoveCommand;
        public CommandBase MoveCommand
        {
            get
            {
                if (_MoveCommand == null)
                {
                    _MoveCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as PLCWriteView;
                        int index = PlcWriteVar.IndexOf(SelectPlcWriteVar);

                        switch (obj)
                        {
                            case "Up":

                                if (index == 0 || index == -1)
                                    return;
                                var temp1 = PlcWriteVar[index - 1];
                                PlcWriteVar[index - 1] = SelectPlcWriteVar;
                                PlcWriteVar[index] = temp1;
                                SelectPlcWriteVar = PlcWriteVar[index - 1];
                                break;
                            case "Down":
                                index = PlcWriteVar.IndexOf(SelectPlcWriteVar);
                                if (index == PlcWriteVar.Count - 1 || index == -1)
                                    return;
                                var temp2 = PlcWriteVar[index + 1];
                                PlcWriteVar[index + 1] = SelectPlcWriteVar;
                                PlcWriteVar[index] = temp2;
                                SelectPlcWriteVar = PlcWriteVar[index + 1];
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _MoveCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "LinkText":
                    LinkValue.Text = obj.LinkName;
                    break;
                default:
                    break;
            }
        }
        #endregion
        #region Method
        public void upDataRemaks()
        {
            if (CurKey==null || CurKey == "") return;
            string[] arr = CurKey.Split('.');
            foreach (var item in Solution.Ins.ProjectList)
            {
                //if (item.ProjectInfo.ProjectID == int.Parse(arr[0]))
                //{
                //    arr[0] = item.ProjectInfo.ProcessName;
                //}
            }
            //Remarks = arr[0] + "." + arr[1];
        }
        #endregion
        [NonSerialized]
        public ExpressionView expressionView;
        [NonSerialized]
        private ObservableCollection<ModuleList> _Modules;
        public ObservableCollection<ModuleList> Modules
        {
            get
            {
                bool flag = this._Modules == null;
                if (flag)
                {
                    this._Modules = new ObservableCollection<ModuleList>();
                }
                return this._Modules;
            }
            set { this._Modules = value; }
        }
    }
}
