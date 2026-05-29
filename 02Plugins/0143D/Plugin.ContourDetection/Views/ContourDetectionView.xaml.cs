using Plugin.ContourDetection.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Core;

namespace Plugin.ContourDetection.Views
{
    /// <summary>
    /// ContourDetectionView.xaml 的交互逻辑
    /// </summary>
    public partial class ContourDetectionView : ModuleViewBase
    {
        public VMHWindowControl SubWindowH;

        public ContourDetectionView()
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
            if (SubWindowH == null)
            {
                SubWindowH = new VMHWindowControl();
                winFormHostSub.Child = SubWindowH;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}
