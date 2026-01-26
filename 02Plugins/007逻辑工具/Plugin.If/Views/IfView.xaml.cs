using System;
using System.Windows;
using HV.Common.Provide;
using HV.Core;
using HV.Models;
using Plugin.If.ViewModels;

namespace Plugin.If.Views
{
    /// <summary>
    /// GrabImageModuleView.xaml 的交互逻辑
    /// </summary>
    public partial class IfView : ModuleViewBase
    {
        public IfView()
        {
            InitializeComponent();
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
            //this.Close();
        }
        
    }
}
