using Plugin.LightControl.ViewModels;
using System.Windows;
using HV.Common.Provide;
using HV.Communacation;
using HV.Core;

namespace Plugin.LightControl.Views
{
    /// <summary>
    /// ReceiveStrView.xaml 的交互逻辑
    /// </summary>
    public partial class LightControlView : ModuleViewBase
    {
        public LightControlView()
        {
            InitializeComponent();
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ModuleViewBase_Activated(object sender, System.EventArgs e)
        {
            InitData();
        }

        private void ModuleViewBase_Loaded(object sender, RoutedEventArgs e)
        {
            InitData();
        }
        #region Method
        private void InitData()
        {
            var viewModel = DataContext as LightControlViewModel;
            if (viewModel == null) return;
            viewModel.ComKeys = EComManageer.GetKeys();
        }
        #endregion
    }
}
