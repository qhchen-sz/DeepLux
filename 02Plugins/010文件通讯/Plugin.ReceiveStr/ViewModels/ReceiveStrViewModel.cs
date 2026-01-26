using Plugin.ReceiveStr.Views;
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
using HV.ViewModels;
using HV.Views;

namespace Plugin.ReceiveStr.ViewModels
{
    [Category("文件通讯")]
    [DisplayName("接收文本")]
    [ModuleImageName("ReceiveStr")]
    [Serializable]
    public class ReceiveStrViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                if (CurKey == "")
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                EComManageer.GetEcomRecStr(CurKey, out RecStr, ReceiveAsHex);
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
            AddOutputParam("接收文本", "string", RecStr);
        }

        #region Prop
        private string RecStr = "";
        private bool _IsEnableTimeOut = false;
        /// <summary>
        /// 启用超时
        /// </summary>
        public bool IsEnableTimeOut
        {
            get { return _IsEnableTimeOut; }
            set { _IsEnableTimeOut = value; RaisePropertyChanged(); }
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
        private bool _ReceiveAsHex=false;
        public bool ReceiveAsHex
        {
            get { return _ReceiveAsHex; }
            set { _ReceiveAsHex = value; } 
        }
        #endregion

        #region Command
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
                        var view = this.ModuleView as ReceiveStrView;
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
    }
}
