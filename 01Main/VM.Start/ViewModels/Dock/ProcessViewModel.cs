using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using
    HV.Common.Helper;

namespace HV.ViewModels.Dock
{
    public class ProcessViewModel:NotifyPropertyBase
    {
        #region Singleton
        private static readonly ProcessViewModel _instance = new ProcessViewModel();

        private ProcessViewModel()
        {
        }
        public static ProcessViewModel Ins
        {
            get { return _instance; }
        }
        #endregion

        #region Prop
        private double _ProcessTime;
        public double ProcessTime
        {
            get { return _ProcessTime; }
            set { _ProcessTime = value; this.RaisePropertyChanged(); }
        }
        private string _IsDisableText = "禁用", _IsDisableIcon = "\xe627";
        public string IsDisableText
        {
            get { return _IsDisableText; }
            set { _IsDisableText = value; this.RaisePropertyChanged(); }
        }
        public string IsDisableIcon
        {
            get { return _IsDisableIcon; }
            set { _IsDisableIcon = value; this.RaisePropertyChanged(); }
        }
        #endregion
    }
}
