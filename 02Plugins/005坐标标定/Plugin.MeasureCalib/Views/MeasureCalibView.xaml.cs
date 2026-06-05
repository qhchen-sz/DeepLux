using Plugin.MeasureCalib.ViewModels;
using System.Windows;
using HV.Core;

namespace Plugin.MeasureCalib.Views
{
    /// <summary>
    /// MeasureCalibView.xaml 的交互逻辑
    /// </summary>
    public partial class MeasureCalibView : ModuleViewBase
    {
        public MeasureCalibView()
        {
            InitializeComponent();
        }

        private void ModuleViewBase_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
