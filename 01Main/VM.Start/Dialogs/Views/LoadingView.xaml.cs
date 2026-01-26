using MahApps.Metro.Controls;
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
using System.Windows.Shapes;
using
   HV.Common.Enums;
using HV.Dialogs.ViewModels;
using HV.Localization;


namespace HV.Dialogs.Views
{
    /// <summary>
    /// LoadingView.xaml 的交互逻辑
    /// </summary>
    public partial class LoadingView : MetroWindow
    {
        public LoadingView()
        {
            InitializeComponent();
            DataContext = new LoadingViewModel();
        }
       public static LoadingViewModel loadingViewModel { get; set; }
        private static LoadingView _instance;
        public static LoadingView Ins
        {
            get
            {
                Application.Current.Dispatcher.Invoke(() => { _instance = new LoadingView(); });
                return _instance;
            }
        }
        public void LoadingShow(string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    loadingViewModel = DataContext as LoadingViewModel;
                    loadingViewModel.Message = msg;
                    this.Topmost = true;
                    loadingViewModel.ToolBarMsg = Resource.Info;
                    loadingViewModel.Icon = new BitmapImage(new Uri(@"/Assets/Images/Info.png", UriKind.Relative));
                    Show();
                }
                catch (Exception ex)
                {
                }
            });

        }
        public void ChangeStr(string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    loadingViewModel.Message = msg;
                }
                catch (Exception ex)
                {
                }
            });
        }
        public void CloseWindows()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Close();
                }
                catch (Exception ex)
                {
                }
            });
        }

    }
}
