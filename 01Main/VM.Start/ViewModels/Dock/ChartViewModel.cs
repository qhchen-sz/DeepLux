using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HV.Common.Helper;

namespace HV.ViewModels.Dock
{
    public class ChartViewModel : NotifyPropertyBase
    {
        #region Singleton
        private static readonly ChartViewModel _instance = new ChartViewModel();

        private ChartViewModel()
        {
        }
        public static ChartViewModel Ins
        {
            get { return _instance; }
        }
        #endregion
    }
}
