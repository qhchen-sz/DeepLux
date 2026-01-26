using HalconDotNet;
using Plugin.Parallel.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Common.Provide;
using HV.Core;
using HV.Models;
using HV.Services;

namespace Plugin.Parallel.Views
{
    /// <summary>
    /// GrabImageModuleView.xaml 的交互逻辑
    /// </summary>
    public partial class CoordinateView : ModuleViewBase
    {
        public CoordinateView()
        {
            InitializeComponent();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }

}
