using System.Windows;
using System.Windows.Forms;

namespace Plugin.ColorRecognition.Views
{
    public partial class ColorRecognitionView
    {
        public VM.Halcon.VMHWindowControl mWindowH;

        public ColorRecognitionView()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ColorRecognitionViewModel vm)
            {
                vm.ModuleView = this;
                vm.Loaded();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}