using Plugin.ShowChart.ViewModels;
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
using VM.Halcon;
using HV.Core;
using System.Globalization;

namespace Plugin.ShowChart.Views
{
    public enum ChartType
    {
        柱状图,
        饼形图,
        折线图
    }
    /// <summary>
    /// SaveImageView.xaml 的交互逻辑
    /// </summary>
    public partial class ShowChartView : ModuleViewBase
    {
        public ShowChartView()
        {
            InitializeComponent();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();

        }
    }
}
