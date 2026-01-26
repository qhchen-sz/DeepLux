using HalconDotNet;
using Plugin.CalculateOffset.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Common.Provide;
using HV.Core;
using HV.Models;
using HV.Services;

namespace Plugin.CalculateOffset.Views
{
    /// <summary>
    /// GrabImageModuleView.xaml 的交互逻辑
    /// </summary>
    public partial class CalculateOffsetView : ModuleViewBase
    {
        public CalculateOffsetView()
        {
            InitializeComponent();
        }
        public VMHWindowControl mWindowH;
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }

}
