using EventMgrLib;
using Plugin.LightControl.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using HV.Views;

namespace Plugin.LightControl.ViewModels
{
    public enum eEnableEndStr
    {
        Have,
        No,
    }
    public enum SourceBrand
    {
        锐视
    }
    public enum eLinkCommand
    {
        SendStr,
        SendStr2,
    }
    [Category("文件通讯")]
    [DisplayName("光源控制")]
    [ModuleImageName("LightControl")]
    [Serializable]
    public class LightControlViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                EComManageer.ClearEcomRecStr(CurKey);
                if (CurKey == "")
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                    if (eEnableEndstr == eEnableEndStr.Have)
                    {
                        EComManageer.IsSendByHex = IsSendByHex;
                        EComManageer.SendStr(CurKey, SendStr+"\r\n");
                        EComManageer.SendStr(CurKey, SendStr2 + "\r\n");
                }
                    else
                    {
                        EComManageer.IsSendByHex = IsSendByHex;
                        EComManageer.SendStr(CurKey, SendStr);
                        EComManageer.SendStr(CurKey, SendStr2);
                }
                
                if (Continue)
                {
                    Prj.ExeModuleName = "";
                }
                bool sendres = true;
                var task = Task.Run(() =>
                {
                    for (int i = 0; i < 2; i++)
                    {
                        EComManageer.GetEcomRecStr(CurKey, out string RecStr);
                        if (Check(RecStr))
                        {
                            sendres = true;
                            continue;
                        }
                        else
                        {
                            sendres = false;
                            EComManageer.ClearEcomRecStr(CurKey);
                            break;
                        }

                    }
                });

                if (!task.Wait(TimeSpan.FromMilliseconds(100))) // 30秒超时
                {

                }
                if (!sendres)
                {
                    if (eEnableEndstr == eEnableEndStr.Have)
                    {
                        EComManageer.IsSendByHex = IsSendByHex;
                        EComManageer.SendStr(CurKey, SendStr + "\r\n");
                        EComManageer.SendStr(CurKey, SendStr2 + "\r\n");
                    }
                    else
                    {
                        EComManageer.IsSendByHex = IsSendByHex;
                        EComManageer.SendStr(CurKey, SendStr);
                        EComManageer.SendStr(CurKey, SendStr2);
                    }
                }
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
            base.AddOutputParams();
        }

        #region Prop
        private bool _Continue = false;
        public bool Continue
        {
            get { return _Continue; }
            set { _Continue = value; }
        }
        private string _SendStr ="";
        /// <summary>
        /// 发送内容
        /// </summary>
        public string SendStr
        {
            get { return _SendStr; }
            set { _SendStr = value;
                EXrun(_SendStr);
                RaisePropertyChanged(); }
        }
        private string _SendStr2 = "";
        /// <summary>
        /// 发送内容
        /// </summary>
        public string SendStr2
        {
            get { return _SendStr2; }
            set { _SendStr2 = value; RaisePropertyChanged(); }
        }
        private bool _IsSendByHex=false;

        public bool IsSendByHex
        {
            get { return _IsSendByHex; }
            set { _IsSendByHex = value; RaisePropertyChanged(); }   
        }

        private bool _IsEnableTimeOut = false;
        /// <summary>
        /// 启用超时
        /// </summary>
        public bool IsEnableTimeOut
        {
            get { return _IsEnableTimeOut; }
            set { _IsEnableTimeOut = value; RaisePropertyChanged(); }
        }
        private eEnableEndStr _eEnableEndstr = eEnableEndStr.No;
        public eEnableEndStr eEnableEndstr
        {
            get { return _eEnableEndstr; }
            set { _eEnableEndstr = value;}
        }

        private string _CurKey = "";
        /// <summary>
        /// 当前Key
        /// </summary>
        public string CurKey
        {
            get { return _CurKey; }
            set
            {
                Set(ref _CurKey, value, new Action(() => { Remarks = EComManageer.GetRemarks(_CurKey); }));
            }
        }

        private string _LightSelectBrand = SourceBrand.锐视.ToString();
        /// <summary>
        /// 当前Key
        /// </summary>
        public string LightSelectBrand
        {
            get { return _LightSelectBrand; }
            set { _LightSelectBrand = value; }
        }
        private string _Remarks;
        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks
        {
            get { return _Remarks; }
            set
            {
                Set(ref _Remarks, value);
            }
        }
        private List<string> _ComKeys;

        public List<string> ComKeys
        {
            get
            {
                if (_ComKeys == null)
                {
                    _ComKeys = new List<string>();
                }
                return _ComKeys;
            }
            set { _ComKeys = value; RaisePropertyChanged(); }
        }

        private List<string> _LightSourceBrand = Enum.GetNames(typeof(SourceBrand)).ToList();

        public List<string> LightSourceBrand
        {
            get
            {
                if (_LightSourceBrand == null)
                {
                    _LightSourceBrand = new List<string>();
                }
                return _LightSourceBrand;
            }
            set { _LightSourceBrand = value; RaisePropertyChanged(); }
        }

        #endregion

        #region Command
        private void OnVarChanged(VarChangedEventParamModel obj)
        {

            switch (obj.SendName.Split(',')[1])
            {
                case "StringLinkText":
                    SendStr = obj.LinkName;
                    break;
                case "StringLinkText2":
                    SendStr2 = obj.LinkName;
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
                    //以类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.SendStr:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string,double,int,bool");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},StringLinkText");
                                break;
                            case eLinkCommand.SendStr2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string,double,int,bool");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},StringLinkText2");
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
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = this.ModuleView as LightControlView;
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

        #endregion
        #region//方法
        private void EXrun(string str)
        {
            switch (LightSelectBrand)
            {
                case "锐视":
                    var matches = System.Text.RegularExpressions.Regex.Matches(str, @"S\d+");
                    string temp = "";
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Success)
                        {
                            //match.Value.Substring(0, 2);
                            if(match.Value.Length>=3)
                                temp += "#"+ match.Value.Substring(0, 3)+"#";
                            else
                                temp += "#" + match.Value.Substring(0, match.Value.Length-1) + "#";
                            //result.Add(match.Value);
                        }
                    }
                    SendStr2 = temp;



                    break;
                default:
                    break;
            }
        }
        private bool Check(string str)
        {
            bool res = false;
            string temp1 = "";
            string temp2 = "";
            switch (LightSelectBrand)
            {
                case "锐视":
                    var matches = System.Text.RegularExpressions.Regex.Matches(SendStr, @"S\d+");
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Success)
                        {
                            //match.Value.Substring(0, 2);
                            if (match.Value.Length >= 6)
                            {
                                temp1 += "_" + match.Value.Substring(1, 2);
                                temp2+= "_" + match.Value.Substring(1, 5);
                            }
                            else
                            {
                                temp1 += "_" + match.Value.Substring(1, match.Value.Length - 1);
                                temp2 += "_" + match.Value.Substring(1, 5);
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
            if(temp1== str || temp2 == str)
                return true;
            else
                return false;
        }
        #endregion
    }
}
