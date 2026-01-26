using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using HV.ViewModels;
using HV.Common;

namespace HV.Views
{
    /// <summary>
    /// SystemParamView.xaml 的交互逻辑
    /// </summary>
    public partial class ChartsSetView : MetroWindow
    {
        #region Singleton
        private static readonly ChartsSetView _instance = new ChartsSetView();

        private ChartsSetView()
        {
            InitializeComponent();
            this.DataContext = ChartsSetViewModel.Ins;
        }
        public static ChartsSetView Ins
        {
            get { return _instance; }
        }

        #endregion

        #region Prop
        public bool IsClosed { get; set; } = true;
        #endregion

        #region Method
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;  // cancels the window close
            IsClosed = true;
            this.Hide();      // Programmatically hides the window
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

    }
}
