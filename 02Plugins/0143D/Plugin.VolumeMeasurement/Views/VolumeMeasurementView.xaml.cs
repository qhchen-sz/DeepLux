using Plugin.VolumeMeasurement.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Core;

namespace Plugin.VolumeMeasurement.Views
{
    public partial class VolumeMeasurementView : ModuleViewBase
    {
        public VolumeMeasurementView()
        {
            InitializeComponent();
        }

        private void ModuleViewBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (mWindowH == null)
            {
                mWindowH = new VMHWindowControl();
                winFormHost.Child = mWindowH;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
