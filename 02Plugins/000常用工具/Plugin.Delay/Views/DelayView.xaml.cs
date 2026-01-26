using System.Windows;
using HV.Common.Provide;
using HV.Core;

namespace Plugin.Delay.Views
{
    /// <summary>
    /// DelayView.xaml 的交互逻辑
    /// </summary>
    public partial class DelayView : ModuleViewBase
    {
        public DelayView()
        {
            InitializeComponent();

        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
