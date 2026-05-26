using System.Windows;
using HV.Core;
using VM.Halcon;

namespace Plugin.ImageOperation.Views
{
    public partial class ImageOperationView : ModuleViewBase
    {
        public ImageOperationView()
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
