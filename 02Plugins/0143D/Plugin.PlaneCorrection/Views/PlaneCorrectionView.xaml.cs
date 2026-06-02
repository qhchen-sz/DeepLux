using Plugin.PlaneCorrection.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Core;

namespace Plugin.PlaneCorrection.Views
{
    public partial class PlaneCorrectionView : ModuleViewBase
    {
        public PlaneCorrectionView()
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
            this.Close();
        }
    }
}
