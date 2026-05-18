using Plugin.GSD.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Core;

namespace Plugin.GSD.Views
{
    /// <summary>
    /// GSDView.xaml 的交互逻辑
    /// </summary>
    public partial class GSDView : ModuleViewBase
    {
        public GSDView()
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
