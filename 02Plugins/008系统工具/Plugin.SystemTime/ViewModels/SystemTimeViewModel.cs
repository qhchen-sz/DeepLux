using EventMgrLib;
using Plugin.SystemTime.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views;

namespace Plugin.SystemTime.ViewModels
{
    #region enum
    #endregion

    [Category("系统工具")]
    [DisplayName("系统时间")]
    [ModuleImageName("SystemTime")]
    [Serializable]
    public class SystemTimeViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                DateTime currentTime = System.DateTime.Now;
                nYear = currentTime.Year;
                nMonth = currentTime.Month;
                nDay = currentTime.Day;
                nHour = currentTime.Hour;
                nMinute = currentTime.Minute;
                nSecond = currentTime.Second;
                nMillisecond = currentTime.Millisecond;
                string sY = "", sH = "", sM = "";
                if (IsYear)
                    sY = currentTime.ToString("yyyy/MM/dd") + " ";
                if (IsHour)
                    sH = currentTime.ToString("HH:mm:ss") + " ";
                if (IsMillisecond)
                    sM = nMillisecond.ToString();
                ResultTime = sY + sH + sM;
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
            AddOutputParam("年", "int", nYear);
            AddOutputParam("月", "int", nMonth);
            AddOutputParam("日", "int", nDay);
            AddOutputParam("时", "int", nHour);
            AddOutputParam("分", "int", nMinute);
            AddOutputParam("秒", "int", nSecond);
            AddOutputParam("毫秒", "int", nMillisecond);
            AddOutputParam("文本", "string", ResultTime);
        }
        #region Prop
        public int nYear { get; set; }
        public int nMonth { get; set; }
        public int nDay { get; set; }
        public int nHour { get; set; }
        public int nMinute { get; set; }
        public int nSecond { get; set; }
        public int nMillisecond { get; set; }

        private bool _IsYear;
        /// <summary>
        /// 年月日
        /// </summary>
        public bool IsYear
        {
            get { return _IsYear; }
            set { _IsYear = value; RaisePropertyChanged(); }
        }

        private bool _IsHour;
        /// <summary>
        /// 时分秒
        /// </summary>
        public bool IsHour
        {
            get { return _IsHour; }
            set { _IsHour = value; RaisePropertyChanged(); }
        }
        private bool _IsMillisecond;
        /// <summary>
        /// 毫秒
        /// </summary>
        public bool IsMillisecond
        {
            get { return _IsMillisecond; }
            set { _IsMillisecond = value; RaisePropertyChanged(); }
        }
        private string _ResultTime = "";
        /// <summary>
        /// 结果文本
        /// </summary>
        public string ResultTime
        {
            get { return _ResultTime; }
            set { _ResultTime = value; RaisePropertyChanged(); }
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
                        var view = this.ModuleView as SystemTimeView;
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
        #region Method

        #endregion
    }
}
