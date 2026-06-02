using Plugin.CropImage.ViewModels;
using System;
using System.Windows;
using VM.Halcon;
using HV.Core;

namespace Plugin.CropImage.Views
{
    public partial class CropImageView : ModuleViewBase
    {
        public CropImageView()
        {
            InitializeComponent();
        }

        public VMHWindowControl mWindowH;

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as CropImageViewModel)?.CleanupMouseEvents();
            this.Close();
        }
    }
}
