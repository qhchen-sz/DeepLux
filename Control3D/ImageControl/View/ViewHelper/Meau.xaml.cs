using LogModule;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageControl
{
    /// <summary>
    /// Meau.xaml 的交互逻辑
    /// </summary>
    public partial class Meau : UserControl
    {
        // 定义事件
        public event EventHandler<string> OnImageLoaded;
        public event EventHandler<bool> Disp_Button;
        public event EventHandler<RoutedEventArgs> FitWindows;
        public event EventHandler<RoutedEventArgs> AddLightimg;
        public Meau()
        {
            InitializeComponent();

        }

        private void menu_OpenFilm_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg;*.tif;*.tiff)|*.png;*.jpeg;*.jpg;*.tif;*.tiff|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                // 触发事件
                OnImageLoaded?.Invoke(this, openFileDialog.FileName);
            }
            Disp3D.Visibility = Visibility.Visible;
            Disp2D.Visibility = Visibility.Collapsed;
        }
        private void Disp_3D(object sender, RoutedEventArgs e)
        {
            // 触发事件
            Disp_Button?.Invoke(this, true);
            Disp3D.Visibility = Visibility.Collapsed;
            Disp2D.Visibility = Visibility.Visible;
        }
        private void Disp_2D(object sender, RoutedEventArgs e)
        {
            // 触发事件
            Disp_Button?.Invoke(this, false);
            Disp3D.Visibility = Visibility.Visible;
            Disp2D.Visibility = Visibility.Collapsed;
        }
        private void menu_Fit_Click(object sender, RoutedEventArgs e)
        {
            // 触发事件
            FitWindows?.Invoke(sender, e);
        }
        private void menu_AddLightimg_Click(object sender, RoutedEventArgs e)
        {
            // 触发事件
            AddLightimg?.Invoke(sender, e);
        }

    }
}
