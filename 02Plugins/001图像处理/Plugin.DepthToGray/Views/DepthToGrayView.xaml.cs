using Plugin.DepthToGray.ViewModels;
using System;
using System.Windows;
using VM.Halcon;
using HV.Core;

namespace Plugin.DepthToGray.Views
{
    public partial class DepthToGrayView : ModuleViewBase
    {
        public DepthToGrayView()
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
