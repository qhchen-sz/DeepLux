using System;
using System.Linq;
using



   HV.Common.Helper;
using HV.Core;
using HV.UIDesign.Dialog.Views;
using HV.ViewModels;

namespace HV.UIDesign.Dialog.ViewModels
{
    public class AxisSelectViewModel : NotifyPropertyBase
    {
        private AxisSelectViewModel() { }

        public static AxisSelectViewModel Ins
        {
            get { return AxisSelectViewModel._instance; }
        }

        public string SelectedMotionName
        {
            get { return this._SelectedMotionName; }
            set
            {
                this._SelectedMotionName = value;
                base.RaisePropertyChanged("SelectedMotionName");
            }
        }

        public string SelectedAxisName
        {
            get { return this._SelectedAxisName; }
            set
            {
                this._SelectedAxisName = value;
                base.RaisePropertyChanged("SelectedAxisName");
            }
        }

        public CommandBase ActivatedCommand
        {
            get
            {
                if (this._ActivatedCommand == null)
                {
                    this._ActivatedCommand = new CommandBase(
                        delegate(object obj)
                        {
                            if (AxisSelectView.Ins.IsClosed)
                            {
                                AxisSelectView.Ins.IsClosed = false;
                                AxisSelectView.Ins.combMotions.ItemsSource =
                                    from o in HardwareConfigViewModel.Ins.MotionModels
                                    select o.MotionNo;
                                if (HardwareConfigViewModel.Ins.SelectedMotion != null)
                                {
                                    AxisSelectView.Ins.combAxis.ItemsSource =
                                        from o in HardwareConfigViewModel.Ins.SelectedMotion.Axis
                                        select o.AxisName;
                                }
                            }
                        }
                    );
                }
                return this._ActivatedCommand;
            }
        }

        public CommandBase ConfirmCommand
        {
            get
            {
                if (this._ConfirmCommand == null)
                {
                    this._ConfirmCommand = new CommandBase(
                        delegate(object obj)
                        {
                            this.ResultLinkData =
                                this.SelectedMotionName + "." + this.SelectedAxisName;
                            AxisSelectView.Ins.Close();
                        }
                    );
                }
                return this._ConfirmCommand;
            }
        }

        private static readonly AxisSelectViewModel _instance = new AxisSelectViewModel();

        public string ResultLinkData = "";

        [NonSerialized]
        private string _SelectedMotionName;

        [NonSerialized]
        private string _SelectedAxisName;

        [NonSerialized]
        private CommandBase _ActivatedCommand;

        [NonSerialized]
        private CommandBase _ConfirmCommand;
    }
}
