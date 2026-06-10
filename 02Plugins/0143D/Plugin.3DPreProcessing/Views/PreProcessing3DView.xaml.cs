using Plugin._3DPreProcessing.ViewModels;
using System.Windows;
using System.Windows.Input;
using VM.Halcon;
using VM.Halcon.Config;
using HV.Core;

namespace Plugin._3DPreProcessing.Views
{
    public partial class PreProcessing3DView : ModuleViewBase
    {
        public PreProcessing3DView()
        {
            InitializeComponent();
        }

        private void ModuleViewBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (mWindowH == null)
            {
                mWindowH = new VMHWindowControl();
                winFormHost.Child = mWindowH;
            }
        }

        private void btnComp_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel1 = DataContext as PreProcessing3DViewModel;
            if (viewModel1 == null) return;
            if (viewModel1.DispImage != null && viewModel1.DispImage.IsInitialized())
                mWindowH.Image = viewModel1.DispImage;
            mWindowH.DispObj(mWindowH.Image);
        }

        private void btnComp_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel1 = DataContext as PreProcessing3DViewModel;
            if (viewModel1 == null) return;
            viewModel1.ExeModule();
            if (viewModel1.m_outImage != null && viewModel1.m_outImage.IsInitialized())
                mWindowH.Image = new RImage(viewModel1.m_outImage);
            mWindowH.DispObj(mWindowH.Image);
        }
    }
}
