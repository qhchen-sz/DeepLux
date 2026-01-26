using System.Windows;
using HV.Common.Provide;
using HV.Core;

namespace Plugin.While.Views
{
    /// <summary>
    /// GrabImageModuleView.xaml 的交互逻辑
    /// </summary>
    public partial class WhileView : ModuleViewBase
    {
        public WhileView()
        {
            InitializeComponent();
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
