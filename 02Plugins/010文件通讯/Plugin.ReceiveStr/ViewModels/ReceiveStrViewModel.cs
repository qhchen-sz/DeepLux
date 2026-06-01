using Newtonsoft.Json.Linq;
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
                if (IsClearCache)
                {
                    EComManageer.ClearEcomRecStr(CurKey);
                }
                bool result;
                if (IsEnableTimeOut)
                {
                    result = EComManageer.GetEcomRecStr(CurKey, out RecStr, ReceiveAsHex, TimeOut);
                }
                else
                {
                    result = EComManageer.GetEcomRecStr(CurKey, out RecStr, ReceiveAsHex);
                }
                if (!result)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
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
        private int _TimeOut = 1000;
        /// <summary>
        /// 超时时间(ms)
        /// </summary>
        public int TimeOut
        {
            get { return _TimeOut; }
            set { _TimeOut = value; RaisePropertyChanged(); }
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
        private bool _IsClearCache = false;
        /// <summary>
        /// 运行前清除缓存
        /// </summary>
        public bool IsClearCache
        {
            get { return _IsClearCache; }
            set { _IsClearCache = value; RaisePropertyChanged(); }
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

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["IsEnableTimeOut"] = IsEnableTimeOut;
            obj["TimeOut"] = TimeOut;
            obj["CurKey"] = CurKey ?? "";
            obj["Remarks"] = Remarks ?? "";
            obj["ReceiveAsHex"] = ReceiveAsHex;
            obj["IsClearCache"] = IsClearCache;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["IsEnableTimeOut"] != null) IsEnableTimeOut = obj["IsEnableTimeOut"].Value<bool>();
                if (obj["TimeOut"] != null) TimeOut = obj["TimeOut"].Value<int>();
                if (obj["CurKey"] != null) CurKey = obj["CurKey"].ToString();
                if (obj["Remarks"] != null) Remarks = obj["Remarks"].ToString();
                if (obj["ReceiveAsHex"] != null) ReceiveAsHex = obj["ReceiveAsHex"].Value<bool>();
                if (obj["IsClearCache"] != null) IsClearCache = obj["IsClearCache"].Value<bool>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"ReceiveStrViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
}
