using System.Windows;
using HV.Core;
using VM.Halcon;

namespace Plugin.BumpDentDetect.Views
{
    public partial class BumpDentDetectView : ModuleViewBase
    {
        public BumpDentDetectView()
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
