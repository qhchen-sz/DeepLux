using Plugin.ImageMerge.ViewModels;
using System.Windows;
using HV.Core;

namespace Plugin.ImageMerge.Views
{
    public partial class ImageMergeView : ModuleViewBase
    {
        public ImageMergeView()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ImageMergeViewModel)?.CleanupMouseEvents();
            Close();
        }
    }
}
