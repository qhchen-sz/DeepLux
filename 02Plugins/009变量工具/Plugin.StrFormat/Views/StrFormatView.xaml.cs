
using Plugin.StrFormat.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Common.Provide;
using HV.Core;
using HV.Models;
using HV.Services;

namespace Plugin.StrFormat.Views
{
    /// <summary>
    /// GrabImageModuleView.xaml 的交互逻辑
    /// </summary>
    public partial class StrFormatView : ModuleViewBase
    {
        public StrFormatView()
        {
            InitializeComponent();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void dg_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextView textView = new TextView();
            if (dg.SelectedItem == null) return;
            textView.DataContext = this.DataContext;
            textView.ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TextView textView = new TextView();
            if (dg.SelectedItem == null) return;
            textView.DataContext = this.DataContext;
            textView.ShowDialog();
        }

        private void dg_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }

}
