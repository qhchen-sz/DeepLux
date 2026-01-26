using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using NLog;
using System.Collections.Concurrent;
using
    HV.ViewModels;
using HalconDotNet;
using HV.Views.Dock;
using HV.Dialogs.Views;

namespace HV.Views
{
    /// <summary>
    /// MainView.xaml 的交互逻辑
    /// </summary>
    public partial class MainView : MetroWindow
    {
        #region Singleton

        private static readonly MainView _instance = new MainView();

        private MainView()
        {
            InitializeComponent();
            this.DataContext = MainViewModel.Ins;
        }
        public async void OpenFile()
        {
            var temp = HV.PersistentData.SystemConfig.Ins;
            if (temp.SolutionPathText != null)
            {
                var view = LoadingView.Ins;
                view.LoadingShow("加载项目中，请稍等...");
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() => HV.ViewModels.MainViewModel.Ins.OpenSolution(temp.SolutionPathText));
                });
                view.Close();
            }
        }
        public static MainView Ins
        {
            get { return _instance; }
        }

        #endregion

        private void window_Activated(object sender, EventArgs e) { }

        private void LaunchVMSite(object sender, RoutedEventArgs e)
        {
            //Process.Start(new ProcessStartInfo
            //{
            //    FileName = "http://www.VMlaser.com/",
            //    // UseShellExecute is default to false on .NET Core while true on .NET Framework.
            //    // Only this value is set to true, the url link can be opened.
            //    UseShellExecute = true,
            //});
        }

        private void tbBarcode_KeyDown(object sender, KeyEventArgs e) { }

        private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            MainViewModel.Ins.LayoutStatusChanged();
        }
    }
}
