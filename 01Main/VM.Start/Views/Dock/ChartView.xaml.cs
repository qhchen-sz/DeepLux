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
using HV.Common.Enums;
using HV.ViewModels.Dock;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using LiveCharts;
using LiveCharts.Wpf;
using Separator = LiveCharts.Wpf.Separator;
using HV.Services;
using HV.ViewModels;

namespace HV.Views.Dock
{
    /// <summary>
    /// DataView.xaml 的交互逻辑
    /// </summary>
    public partial class ChartView : System.Windows.Controls.UserControl
    {
        #region Singleton
        private static readonly ChartView _instance = new ChartView();
        private ChartView()
        {
            InitializeComponent();
            this.DataContext = VisionViewModel.Ins;

            //Dispatcher.BeginInvoke(new Action(() =>
            //{
            //    for (int i = 1; i <= 9; i++)
            //    {
            //        GetChartBox(i);
            //    }
            //    ShowCanvasAll();
            //}));

            //for (int i = 1; i <= 9; i++)
            //{
            //    GetChartBox(i);
            //}
            InitChartBox();
            ShowCanvasAll();
        }

        public static ChartView Ins
        {
            get { return _instance; }
        }
        #endregion
        #region Prop
        private eViewMode _ViewMode = eViewMode.One;
        public eViewMode ViewMode
        {
            get { return _ViewMode; }
            set
            {
                _ViewMode = value;
                ShowCanvasAll();
            }
        }

        #endregion
        #region Method
        public object GetChartBox(int key,bool IsChange =false)
        {
            if(!IsChange)
            {
                return ChartDic.mChartDic[key];
            }
            Types chartType = Types.柱状图;
            switch (key)
            {
                case 1:
                    chartType = ChartsSetViewModel.Ins.ChartType1;
                    break;
                case 2:
                    chartType = ChartsSetViewModel.Ins.ChartType2;
                    break;
                case 3:
                    chartType = ChartsSetViewModel.Ins.ChartType3;
                    break;
                case 4:
                    chartType = ChartsSetViewModel.Ins.ChartType4;
                    break;
                case 5:
                    chartType = ChartsSetViewModel.Ins.ChartType5;
                    break;
                case 6:
                    chartType = ChartsSetViewModel.Ins.ChartType6;
                    break;
                case 7:
                    chartType = ChartsSetViewModel.Ins.ChartType7;
                    break;
                case 8:
                    chartType = ChartsSetViewModel.Ins.ChartType8;
                    break;
                case 9:
                    chartType = ChartsSetViewModel.Ins.ChartType9;
                    break;
            }

            switch (chartType)
            {
                case Types.柱状图:
                    if (!ChartDic.mChartDic.ContainsKey(key))
                    {
                        CartesianChart mChart = new CartesianChart();
                        SetColumnChart(mChart);
                        ChartDic.mChartDic.Add(key, mChart);
                    }
                    else
                    {
                        ChartDic.mChartDic[key] = new CartesianChart();
                        SetColumnChart((CartesianChart)ChartDic.mChartDic[key]);
                    }
                    break;
                case Types.折线图:
                    if (!ChartDic.mChartDic.ContainsKey(key))
                    {
                        CartesianChart mChart = new CartesianChart();
                        SetLineChart(mChart);
                        ChartDic.mChartDic.Add(key, mChart);
                    }
                    else
                    {
                        ChartDic.mChartDic[key] = new CartesianChart();
                        SetLineChart((CartesianChart)ChartDic.mChartDic[key]);
                    }
                    break;

                case Types.饼图:
                    if (!ChartDic.mChartDic.ContainsKey(key))
                    {
                        PieChart mChart = new PieChart();
                        SetPieChart(mChart);
                        ChartDic.mChartDic.Add(key, mChart);
                    }
                    else
                    {
                        ChartDic.mChartDic[key] = new PieChart();
                        SetPieChart((PieChart)ChartDic.mChartDic[key]);
                    }
                    break;
            }

            return ChartDic.mChartDic[key];
        }

        public void InitChartBox()
        {
            for (int i = 1; i <= 9; i++)
            {
                CartesianChart mChart = new CartesianChart();
                ChartDic.mChartDic.Add(i, mChart);
            }
        }
        //public void SetChartValue(object chart, int index)
        //{
        //    Types chartType = (Types)(index + 1);
        //    switch (chartType)
        //    {
        //        case Types.柱状图:
        //            if (chart is CartesianChart cartesianChart)
        //            {
        //                SetColumnChart(cartesianChart, index);
        //            }
        //            break;

        //        case Types.折线图:
        //            if (chart is CartesianChart lineChart)
        //            {
        //                SetLineChart(lineChart, index);
        //            }
        //            break;

        //        case Types.饼图:
        //            if (chart is PieChart pieChart)
        //            {
        //                SetPieChart(pieChart, index);
        //            }
        //            break;
        //    }
        //}
        // 设置柱状图
        private void SetColumnChart(CartesianChart chart)
        {
            // 示例数据 - 实际应用中应从数据源获取
            var series = new ColumnSeries
            {
                Values = new ChartValues<double> { 10, 20, 30 },
                Title = $"OK",
                DataLabels = true,
                LabelPoint = point => $"{point.Y:N0}",
                FontSize = 12,
                Fill = new SolidColorBrush(Color.FromRgb(50, 205, 50)),
                Foreground = Brushes.White,
            };

            var series2 = new ColumnSeries
            {
                Values = new ChartValues<double> { 40, 50, 60 },
                Title = $"NG",
                DataLabels = true,
                LabelPoint = point => $"{point.Y:N0}",
                FontSize = 12,
                Foreground = Brushes.White,
                Fill = new SolidColorBrush(Color.FromRgb(220, 20, 60)),
            };

            chart.Series = new SeriesCollection { series, series2 };

            chart.AxisX.Add(new Axis
            {
                Labels = new[] { "A", "B", "C" },
                Separator = new Separator { StrokeThickness = 0 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "数值",
                Separator = new Separator { StrokeThickness = 0 }
            });
        }

        // 设置折线图
        private void SetLineChart(CartesianChart chart)
        {
            // 示例数据 - 实际应用中应从数据源获取
            var lineSeries = new LineSeries
            {
                Title = $"OK",
                Values = new ChartValues<double> { 15, 22, 18, 25, 20 },
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 10,
                Stroke = Brushes.Green,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                DataLabels = true,
                LabelPoint = point => $"{point.Y:N1}",
                FontSize = 10,
                Foreground = Brushes.White,
            };
            var lineSeries2 = new LineSeries
            {
                Title = $"NG",
                Values = new ChartValues<double> { 3, 2, 1, 6, 7 },
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 10,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                DataLabels = true,
                LabelPoint = point => $"{point.Y:N1}",
                FontSize = 10,
                Foreground = Brushes.White,
            };
            chart.Series = new SeriesCollection { lineSeries, lineSeries2 };

            chart.AxisX.Add(new Axis
            {
                Title = "时间",
                Labels = new[] { "1月", "2月", "3月", "4月", "5月" },
                Separator = new Separator { StrokeThickness = 0 }
            });

            chart.AxisY.Add(new Axis
            {
                Title = "数值",
                Separator = new Separator { StrokeThickness = 0 }
            });
        }

        // 设置饼图
        private void SetPieChart(PieChart chart)
        {
            // 创建饼图系列
            var pieSeries = new PieSeries
            {
                Title = $"OK",
                Values = new ChartValues<double> { 220 },
                DataLabels = true,
                LabelPoint = point => $"{point.SeriesView.Title}:{ point.Y } ",
                FontSize = 12,
                Foreground = Brushes.White,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            var pieSeries2 = new PieSeries
            {
                Title = $"NG",
                Values = new ChartValues<double> { 30 },
                DataLabels = true,
                LabelPoint = point => $"{point.SeriesView.Title}:{point.Y} ",
                FontSize = 12,
                Foreground = Brushes.White,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            // 设置颜色
            var colors = new[]
                    {
                Brushes.Tomato,
                Brushes.Gold,
                Brushes.MediumSeaGreen,
                Brushes.SteelBlue
            };

            for (int i = 0; i < pieSeries.Values.Count; i++)
            {
                pieSeries.Fill = colors[i % colors.Length];
            }

            chart.Series = new SeriesCollection { pieSeries ,pieSeries2 };
        }

        // 创建事件处理方法

        private void ShowCanvasAll()
        {
            RowDefinition row1 = new RowDefinition();
            RowDefinition row2 = new RowDefinition();
            RowDefinition row3 = new RowDefinition();
            ColumnDefinition col1 = new ColumnDefinition();
            ColumnDefinition col2 = new ColumnDefinition();
            ColumnDefinition col3 = new ColumnDefinition();
            ColumnDefinition col4 = new ColumnDefinition();
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();
            switch (_ViewMode)
            {
                case eViewMode.One:
                    var chart1 = GetChartBox(1,true);
                    if(chart1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart1);
                        Grid.SetRow((PieChart)chart1, 0);
                        Grid.SetColumn((PieChart)chart1, 0);
                    }
                    else 
                    {
                        grid.Children.Add((CartesianChart)chart1);
                        Grid.SetRow((CartesianChart)chart1, 0);
                        Grid.SetColumn((CartesianChart)chart1, 0);
                    }

                    break;
                case eViewMode.Two:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    var chart2_1 = GetChartBox(1, true);
                    if (chart2_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart2_1);
                        Grid.SetRow((PieChart)chart2_1, 0);
                        Grid.SetColumn((PieChart)chart2_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart2_1);
                        Grid.SetRow((CartesianChart)chart2_1, 0);
                        Grid.SetColumn((CartesianChart)chart2_1, 0);
                    }


                    var chart2_2 = GetChartBox(2, true);
                    if (chart2_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart2_2);
                        Grid.SetRow((PieChart)chart2_2, 0);
                        Grid.SetColumn((PieChart)chart2_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart2_2);
                        Grid.SetRow((CartesianChart)chart2_2, 0);
                        Grid.SetColumn((CartesianChart)chart2_2, 1);
                    }


                    break;
                case eViewMode.Three:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart3_1 = GetChartBox(1, true);
                    if (chart3_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart3_1);
                        Grid.SetRow((PieChart)chart3_1, 0);
                        Grid.SetColumn((PieChart)chart3_1, 0);
                        Grid.SetRowSpan((PieChart)chart3_1, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart3_1);
                        Grid.SetRow((CartesianChart)chart3_1, 0);
                        Grid.SetColumn((CartesianChart)chart3_1, 0);
                        Grid.SetRowSpan((CartesianChart)chart3_1, 2);
                    }


                    var chart3_2 = GetChartBox(2, true);
                    if (chart3_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart3_2);
                        Grid.SetRow((PieChart)chart3_2, 0);
                        Grid.SetColumn((PieChart)chart3_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart3_2);
                        Grid.SetRow((CartesianChart)chart3_2, 0);
                        Grid.SetColumn((CartesianChart)chart3_2, 1);
                    }


                    var chart3_3 = GetChartBox(3, true);
                    if (chart3_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart3_3);
                        Grid.SetRow((PieChart)chart3_3, 1);
                        Grid.SetColumn((PieChart)chart3_3, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart3_3);
                        Grid.SetRow((CartesianChart)chart3_3, 1);
                        Grid.SetColumn((CartesianChart)chart3_3, 1);
                    }


                    break;
                case eViewMode.Four:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    var chart4_1 = GetChartBox(1, true);
                    if (chart4_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_1);
                        Grid.SetRow((PieChart)chart4_1, 0);
                        Grid.SetColumn((PieChart)chart4_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_1);
                        Grid.SetRow((CartesianChart)chart4_1, 0);
                        Grid.SetColumn((CartesianChart)chart4_1, 0);
                    }


                    var chart4_2 = GetChartBox(2, true);
                    if (chart4_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_2);
                        Grid.SetRow((PieChart)chart4_2, 0);
                        Grid.SetColumn((PieChart)chart4_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_2);
                        Grid.SetRow((CartesianChart)chart4_2, 0);
                        Grid.SetColumn((CartesianChart)chart4_2, 1);
                    }


                    var chart4_3 = GetChartBox(3, true);
                    if (chart4_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_3);
                        Grid.SetRow((PieChart)chart4_3, 1);
                        Grid.SetColumn((PieChart)chart4_3, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_3);
                        Grid.SetRow((CartesianChart)chart4_3, 1);
                        Grid.SetColumn((CartesianChart)chart4_3, 0);
                    }


                    var chart4_4 = GetChartBox(4, true);
                    if (chart4_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_4);
                        Grid.SetRow((PieChart)chart4_4, 1);
                        Grid.SetColumn((PieChart)chart4_4, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_4);
                        Grid.SetRow((CartesianChart)chart4_4, 1);
                        Grid.SetColumn((CartesianChart)chart4_4, 1);
                    }


                    break;
                case eViewMode.Five:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart5_1 = GetChartBox(1, true);
                    if (chart5_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_1);
                        Grid.SetRow((PieChart)chart5_1, 0);
                        Grid.SetColumn((PieChart)chart5_1, 0);
                        Grid.SetColumnSpan((PieChart)chart5_1, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_1);
                        Grid.SetRow((CartesianChart)chart5_1, 0);
                        Grid.SetColumn((CartesianChart)chart5_1, 0);
                        Grid.SetColumnSpan((CartesianChart)chart5_1, 2);
                    }


                    var chart5_2 = GetChartBox(2, true);
                    if (chart5_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_2);
                        Grid.SetRow((PieChart)chart5_2, 0);
                        Grid.SetColumn((PieChart)chart5_2, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_2);
                        Grid.SetRow((CartesianChart)chart5_2, 0);
                        Grid.SetColumn((CartesianChart)chart5_2, 2);
                    }


                    var chart5_3 = GetChartBox(3, true);
                    if (chart5_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_3);
                        Grid.SetRow((PieChart)chart5_3, 1);
                        Grid.SetColumn((PieChart)chart5_3, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_3);
                        Grid.SetRow((CartesianChart)chart5_3, 1);
                        Grid.SetColumn((CartesianChart)chart5_3, 0);
                    }


                    var chart5_4 = GetChartBox(4, true);
                    if (chart5_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_4);
                        Grid.SetRow((PieChart)chart5_4, 1);
                        Grid.SetColumn((PieChart)chart5_4, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_4);
                        Grid.SetRow((CartesianChart)chart5_4, 1);
                        Grid.SetColumn((CartesianChart)chart5_4, 1);
                    }


                    var chart5_5 = GetChartBox(5, true);
                    if (chart5_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_5);
                        Grid.SetRow((PieChart)chart5_5, 1);
                        Grid.SetColumn((PieChart)chart5_5, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_5);
                        Grid.SetRow((CartesianChart)chart5_5, 1);
                        Grid.SetColumn((CartesianChart)chart5_5, 2);
                    }

                    break;
                case eViewMode.Six:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart6_1 = GetChartBox(1, true);
                    if (chart6_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_1);
                        Grid.SetRow((PieChart)chart6_1, 0);
                        Grid.SetColumn((PieChart)chart6_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_1);
                        Grid.SetRow((CartesianChart)chart6_1, 0);
                        Grid.SetColumn((CartesianChart)chart6_1, 0);
                    }


                    var chart6_2 = GetChartBox(2, true);
                    if (chart6_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_2);
                        Grid.SetRow((PieChart)chart6_2, 0);
                        Grid.SetColumn((PieChart)chart6_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_2);
                        Grid.SetRow((CartesianChart)chart6_2, 0);
                        Grid.SetColumn((CartesianChart)chart6_2, 1);
                    }


                    var chart6_3 = GetChartBox(3, true);
                    if (chart6_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_3);
                        Grid.SetRow((PieChart)chart6_3, 0);
                        Grid.SetColumn((PieChart)chart6_3, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_3);
                        Grid.SetRow((CartesianChart)chart6_3, 0);
                        Grid.SetColumn((CartesianChart)chart6_3, 2);
                    }


                    var chart6_4 = GetChartBox(4, true);
                    if (chart6_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_4);
                        Grid.SetRow((PieChart)chart6_4, 1);
                        Grid.SetColumn((PieChart)chart6_4, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_4);
                        Grid.SetRow((CartesianChart)chart6_4, 1);
                        Grid.SetColumn((CartesianChart)chart6_4, 0);
                    }


                    var chart6_5 = GetChartBox(5, true);
                    if (chart6_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_5);
                        Grid.SetRow((PieChart)chart6_5, 1);
                        Grid.SetColumn((PieChart)chart6_5, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_5);
                        Grid.SetRow((CartesianChart)chart6_5, 1);
                        Grid.SetColumn((CartesianChart)chart6_5, 1);
                    }


                    var chart6_6 = GetChartBox(6, true);
                    if (chart6_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_6);
                        Grid.SetRow((PieChart)chart6_6, 1);
                        Grid.SetColumn((PieChart)chart6_6, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_6);
                        Grid.SetRow((CartesianChart)chart6_6, 1);
                        Grid.SetColumn((CartesianChart)chart6_6, 2);
                    }

                    break;
                case eViewMode.Seven:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart7_1 = GetChartBox(1, true);
                    if (chart7_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_1);
                        Grid.SetRow((PieChart)chart7_1, 0);
                        Grid.SetColumn((PieChart)chart7_1, 0);
                        Grid.SetColumnSpan((PieChart)chart7_1, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_1);
                        Grid.SetRow((CartesianChart)chart7_1, 0);
                        Grid.SetColumn((CartesianChart)chart7_1, 0);
                        Grid.SetColumnSpan((CartesianChart)chart7_1, 2);
                    }


                    var chart7_2 = GetChartBox(2, true);
                    if (chart7_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_2);
                        Grid.SetRow((PieChart)chart7_2, 0);
                        Grid.SetColumn((PieChart)chart7_2, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_2);
                        Grid.SetRow((CartesianChart)chart7_2, 0);
                        Grid.SetColumn((CartesianChart)chart7_2, 2);
                    }


                    var chart7_3 = GetChartBox(3, true);
                    if (chart7_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_3);
                        Grid.SetRow((PieChart)chart7_3, 0);
                        Grid.SetColumn((PieChart)chart7_3, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_3);
                        Grid.SetRow((CartesianChart)chart7_3, 0);
                        Grid.SetColumn((CartesianChart)chart7_3, 3);
                    }


                    var chart7_4 = GetChartBox(4, true);
                    if (chart7_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_4);
                        Grid.SetRow((PieChart)chart7_4, 1);
                        Grid.SetColumn((PieChart)chart7_4, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_4);
                        Grid.SetRow((CartesianChart)chart7_4, 1);
                        Grid.SetColumn((CartesianChart)chart7_4, 0);
                    }


                    var chart7_5 = GetChartBox(5, true);
                    if (chart7_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_5);
                        Grid.SetRow((PieChart)chart7_5, 1);
                        Grid.SetColumn((PieChart)chart7_5, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_5);
                        Grid.SetRow((CartesianChart)chart7_5, 1);
                        Grid.SetColumn((CartesianChart)chart7_5, 1);
                    }


                    var chart7_6 = GetChartBox(6, true);
                    if (chart7_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_6);
                        Grid.SetRow((PieChart)chart7_6, 1);
                        Grid.SetColumn((PieChart)chart7_6, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_6);
                        Grid.SetRow((CartesianChart)chart7_6, 1);
                        Grid.SetColumn((CartesianChart)chart7_6, 2);
                    }


                    var chart7_7 = GetChartBox(7, true);
                    if (chart7_7 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_7);
                        Grid.SetRow((PieChart)chart7_7, 1);
                        Grid.SetColumn((PieChart)chart7_7, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_7);
                        Grid.SetRow((CartesianChart)chart7_7, 1);
                        Grid.SetColumn((CartesianChart)chart7_7, 3);
                    }

                    break;
                case eViewMode.Eight:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart8_1 = GetChartBox(1, true);
                    if (chart8_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_1);
                        Grid.SetRow((PieChart)chart8_1, 0);
                        Grid.SetColumn((PieChart)chart8_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_1);
                        Grid.SetRow((CartesianChart)chart8_1, 0);
                        Grid.SetColumn((CartesianChart)chart8_1, 0);
                    }


                    var chart8_2 = GetChartBox(2, true);
                    if (chart8_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_2);
                        Grid.SetRow((PieChart)chart8_2, 0);
                        Grid.SetColumn((PieChart)chart8_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_2);
                        Grid.SetRow((CartesianChart)chart8_2, 0);
                        Grid.SetColumn((CartesianChart)chart8_2, 1);
                    }


                    var chart8_3 = GetChartBox(3, true);
                    if (chart8_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_3);
                        Grid.SetRow((PieChart)chart8_3, 0);
                        Grid.SetColumn((PieChart)chart8_3, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_3);
                        Grid.SetRow((CartesianChart)chart8_3, 0);
                        Grid.SetColumn((CartesianChart)chart8_3, 2);
                    }


                    var chart8_4 = GetChartBox(4, true);
                    if (chart8_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_4);
                        Grid.SetRow((PieChart)chart8_4, 0);
                        Grid.SetColumn((PieChart)chart8_4, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_4);
                        Grid.SetRow((CartesianChart)chart8_4, 0);
                        Grid.SetColumn((CartesianChart)chart8_4, 3);
                    }


                    var chart8_5 = GetChartBox(5, true);
                    if (chart8_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_5);
                        Grid.SetRow((PieChart)chart8_5, 1);
                        Grid.SetColumn((PieChart)chart8_5, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_5);
                        Grid.SetRow((CartesianChart)chart8_5, 1);
                        Grid.SetColumn((CartesianChart)chart8_5, 0);
                    }


                    var chart8_6 = GetChartBox(6, true);
                    if (chart8_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_6);
                        Grid.SetRow((PieChart)chart8_6, 1);
                        Grid.SetColumn((PieChart)chart8_6, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_6);
                        Grid.SetRow((CartesianChart)chart8_6, 1);
                        Grid.SetColumn((CartesianChart)chart8_6, 1);
                    }


                    var chart8_7 = GetChartBox(7, true);
                    if (chart8_7 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_7);
                        Grid.SetRow((PieChart)chart8_7, 1);
                        Grid.SetColumn((PieChart)chart8_7, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_7);
                        Grid.SetRow((CartesianChart)chart8_7, 1);
                        Grid.SetColumn((CartesianChart)chart8_7, 2);
                    }


                    var chart8_8 = GetChartBox(8, true);
                    if (chart8_8 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_8);
                        Grid.SetRow((PieChart)chart8_8, 1);
                        Grid.SetColumn((PieChart)chart8_8, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_8);
                        Grid.SetRow((CartesianChart)chart8_8, 1);
                        Grid.SetColumn((CartesianChart)chart8_8, 3);
                    }

                    break;
                case eViewMode.Night:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    grid.RowDefinitions.Add(row3);

                    var chart9_1 = GetChartBox(1, true);
                    if (chart9_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_1);
                        Grid.SetRow((PieChart)chart9_1, 0);
                        Grid.SetColumn((PieChart)chart9_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_1);
                        Grid.SetRow((CartesianChart)chart9_1, 0);
                        Grid.SetColumn((CartesianChart)chart9_1, 0);
                    }


                    var chart9_2 = GetChartBox(2, true);
                    if (chart9_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_2);
                        Grid.SetRow((PieChart)chart9_2, 0);
                        Grid.SetColumn((PieChart)chart9_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_2);
                        Grid.SetRow((CartesianChart)chart9_2, 0);
                        Grid.SetColumn((CartesianChart)chart9_2, 1);
                    }


                    var chart9_3 = GetChartBox(3, true);
                    if (chart9_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_3);
                        Grid.SetRow((PieChart)chart9_3, 0);
                        Grid.SetColumn((PieChart)chart9_3, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_3);
                        Grid.SetRow((CartesianChart)chart9_3, 0);
                        Grid.SetColumn((CartesianChart)chart9_3, 2);
                    }


                    var chart9_4 = GetChartBox(4, true);
                    if (chart9_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_4);
                        Grid.SetRow((PieChart)chart9_4, 1);
                        Grid.SetColumn((PieChart)chart9_4, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_4);
                        Grid.SetRow((CartesianChart)chart9_4, 1);
                        Grid.SetColumn((CartesianChart)chart9_4, 0);
                    }


                    var chart9_5 = GetChartBox(5, true);
                    if (chart9_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_5);
                        Grid.SetRow((PieChart)chart9_5, 1);
                        Grid.SetColumn((PieChart)chart9_5, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_5);
                        Grid.SetRow((CartesianChart)chart9_5, 1);
                        Grid.SetColumn((CartesianChart)chart9_5, 1);
                    }


                    var chart9_6 = GetChartBox(6, true);
                    if (chart9_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_6);
                        Grid.SetRow((PieChart)chart9_6, 1);
                        Grid.SetColumn((PieChart)chart9_6, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_6);
                        Grid.SetRow((CartesianChart)chart9_6, 1);
                        Grid.SetColumn((CartesianChart)chart9_6, 2);
                    }


                    var chart9_7 = GetChartBox(7, true);
                    if (chart9_7 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_7);
                        Grid.SetRow((PieChart)chart9_7, 2);
                        Grid.SetColumn((PieChart)chart9_7, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_7);
                        Grid.SetRow((CartesianChart)chart9_7, 2);
                        Grid.SetColumn((CartesianChart)chart9_7, 0);
                    }


                    var chart9_8 = GetChartBox(8, true);
                    if (chart9_8 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_8);
                        Grid.SetRow((PieChart)chart9_8, 2);
                        Grid.SetColumn((PieChart)chart9_8, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_8);
                        Grid.SetRow((CartesianChart)chart9_8, 2);
                        Grid.SetColumn((CartesianChart)chart9_8, 1);
                    }


                    var chart9_9 = GetChartBox(9, true);
                    if (chart9_9 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_9);
                        Grid.SetRow((PieChart)chart9_9, 2);
                        Grid.SetColumn((PieChart)chart9_9, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_9);
                        Grid.SetRow((CartesianChart)chart9_9, 2);
                        Grid.SetColumn((CartesianChart)chart9_9, 2);
                    }


                    break;
                default:
                    break;
            }


        }

        public void ResetView()
        {
            RowDefinition row1 = new RowDefinition();
            RowDefinition row2 = new RowDefinition();
            RowDefinition row3 = new RowDefinition();
            ColumnDefinition col1 = new ColumnDefinition();
            ColumnDefinition col2 = new ColumnDefinition();
            ColumnDefinition col3 = new ColumnDefinition();
            ColumnDefinition col4 = new ColumnDefinition();
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();
            switch (_ViewMode)
            {
                case eViewMode.One:
                    var chart1 = GetChartBox(1);
                    if (chart1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart1);
                        Grid.SetRow((PieChart)chart1, 0);
                        Grid.SetColumn((PieChart)chart1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart1);
                        Grid.SetRow((CartesianChart)chart1, 0);
                        Grid.SetColumn((CartesianChart)chart1, 0);
                    }

                    break;
                case eViewMode.Two:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    var chart2_1 = GetChartBox(1);
                    if (chart2_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart2_1);
                        Grid.SetRow((PieChart)chart2_1, 0);
                        Grid.SetColumn((PieChart)chart2_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart2_1);
                        Grid.SetRow((CartesianChart)chart2_1, 0);
                        Grid.SetColumn((CartesianChart)chart2_1, 0);
                    }


                    var chart2_2 = GetChartBox(2);
                    if (chart2_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart2_2);
                        Grid.SetRow((PieChart)chart2_2, 0);
                        Grid.SetColumn((PieChart)chart2_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart2_2);
                        Grid.SetRow((CartesianChart)chart2_2, 0);
                        Grid.SetColumn((CartesianChart)chart2_2, 1);
                    }


                    break;
                case eViewMode.Three:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart3_1 = GetChartBox(1);
                    if (chart3_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart3_1);
                        Grid.SetRow((PieChart)chart3_1, 0);
                        Grid.SetColumn((PieChart)chart3_1, 0);
                        Grid.SetRowSpan((PieChart)chart3_1, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart3_1);
                        Grid.SetRow((CartesianChart)chart3_1, 0);
                        Grid.SetColumn((CartesianChart)chart3_1, 0);
                        Grid.SetRowSpan((CartesianChart)chart3_1, 2);
                    }


                    var chart3_2 = GetChartBox(2);
                    if (chart3_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart3_2);
                        Grid.SetRow((PieChart)chart3_2, 0);
                        Grid.SetColumn((PieChart)chart3_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart3_2);
                        Grid.SetRow((CartesianChart)chart3_2, 0);
                        Grid.SetColumn((CartesianChart)chart3_2, 1);
                    }


                    var chart3_3 = GetChartBox(3);
                    if (chart3_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart3_3);
                        Grid.SetRow((PieChart)chart3_3, 1);
                        Grid.SetColumn((PieChart)chart3_3, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart3_3);
                        Grid.SetRow((CartesianChart)chart3_3, 1);
                        Grid.SetColumn((CartesianChart)chart3_3, 1);
                    }


                    break;
                case eViewMode.Four:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    var chart4_1 = GetChartBox(1);
                    if (chart4_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_1);
                        Grid.SetRow((PieChart)chart4_1, 0);
                        Grid.SetColumn((PieChart)chart4_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_1);
                        Grid.SetRow((CartesianChart)chart4_1, 0);
                        Grid.SetColumn((CartesianChart)chart4_1, 0);
                    }


                    var chart4_2 = GetChartBox(2);
                    if (chart4_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_2);
                        Grid.SetRow((PieChart)chart4_2, 0);
                        Grid.SetColumn((PieChart)chart4_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_2);
                        Grid.SetRow((CartesianChart)chart4_2, 0);
                        Grid.SetColumn((CartesianChart)chart4_2, 1);
                    }


                    var chart4_3 = GetChartBox(3);
                    if (chart4_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_3);
                        Grid.SetRow((PieChart)chart4_3, 1);
                        Grid.SetColumn((PieChart)chart4_3, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_3);
                        Grid.SetRow((CartesianChart)chart4_3, 1);
                        Grid.SetColumn((CartesianChart)chart4_3, 0);
                    }


                    var chart4_4 = GetChartBox(4);
                    if (chart4_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart4_4);
                        Grid.SetRow((PieChart)chart4_4, 1);
                        Grid.SetColumn((PieChart)chart4_4, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart4_4);
                        Grid.SetRow((CartesianChart)chart4_4, 1);
                        Grid.SetColumn((CartesianChart)chart4_4, 1);
                    }


                    break;
                case eViewMode.Five:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart5_1 = GetChartBox(1);
                    if (chart5_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_1);
                        Grid.SetRow((PieChart)chart5_1, 0);
                        Grid.SetColumn((PieChart)chart5_1, 0);
                        Grid.SetColumnSpan((PieChart)chart5_1, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_1);
                        Grid.SetRow((CartesianChart)chart5_1, 0);
                        Grid.SetColumn((CartesianChart)chart5_1, 0);
                        Grid.SetColumnSpan((CartesianChart)chart5_1, 2);
                    }


                    var chart5_2 = GetChartBox(2);
                    if (chart5_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_2);
                        Grid.SetRow((PieChart)chart5_2, 0);
                        Grid.SetColumn((PieChart)chart5_2, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_2);
                        Grid.SetRow((CartesianChart)chart5_2, 0);
                        Grid.SetColumn((CartesianChart)chart5_2, 2);
                    }


                    var chart5_3 = GetChartBox(3);
                    if (chart5_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_3);
                        Grid.SetRow((PieChart)chart5_3, 1);
                        Grid.SetColumn((PieChart)chart5_3, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_3);
                        Grid.SetRow((CartesianChart)chart5_3, 1);
                        Grid.SetColumn((CartesianChart)chart5_3, 0);
                    }


                    var chart5_4 = GetChartBox(4);
                    if (chart5_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_4);
                        Grid.SetRow((PieChart)chart5_4, 1);
                        Grid.SetColumn((PieChart)chart5_4, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_4);
                        Grid.SetRow((CartesianChart)chart5_4, 1);
                        Grid.SetColumn((CartesianChart)chart5_4, 1);
                    }


                    var chart5_5 = GetChartBox(5);
                    if (chart5_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart5_5);
                        Grid.SetRow((PieChart)chart5_5, 1);
                        Grid.SetColumn((PieChart)chart5_5, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart5_5);
                        Grid.SetRow((CartesianChart)chart5_5, 1);
                        Grid.SetColumn((CartesianChart)chart5_5, 2);
                    }

                    break;
                case eViewMode.Six:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart6_1 = GetChartBox(1);
                    if (chart6_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_1);
                        Grid.SetRow((PieChart)chart6_1, 0);
                        Grid.SetColumn((PieChart)chart6_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_1);
                        Grid.SetRow((CartesianChart)chart6_1, 0);
                        Grid.SetColumn((CartesianChart)chart6_1, 0);
                    }


                    var chart6_2 = GetChartBox(2);
                    if (chart6_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_2);
                        Grid.SetRow((PieChart)chart6_2, 0);
                        Grid.SetColumn((PieChart)chart6_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_2);
                        Grid.SetRow((CartesianChart)chart6_2, 0);
                        Grid.SetColumn((CartesianChart)chart6_2, 1);
                    }


                    var chart6_3 = GetChartBox(3);
                    if (chart6_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_3);
                        Grid.SetRow((PieChart)chart6_3, 0);
                        Grid.SetColumn((PieChart)chart6_3, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_3);
                        Grid.SetRow((CartesianChart)chart6_3, 0);
                        Grid.SetColumn((CartesianChart)chart6_3, 2);
                    }


                    var chart6_4 = GetChartBox(4);
                    if (chart6_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_4);
                        Grid.SetRow((PieChart)chart6_4, 1);
                        Grid.SetColumn((PieChart)chart6_4, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_4);
                        Grid.SetRow((CartesianChart)chart6_4, 1);
                        Grid.SetColumn((CartesianChart)chart6_4, 0);
                    }


                    var chart6_5 = GetChartBox(5);
                    if (chart6_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_5);
                        Grid.SetRow((PieChart)chart6_5, 1);
                        Grid.SetColumn((PieChart)chart6_5, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_5);
                        Grid.SetRow((CartesianChart)chart6_5, 1);
                        Grid.SetColumn((CartesianChart)chart6_5, 1);
                    }


                    var chart6_6 = GetChartBox(6);
                    if (chart6_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart6_6);
                        Grid.SetRow((PieChart)chart6_6, 1);
                        Grid.SetColumn((PieChart)chart6_6, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart6_6);
                        Grid.SetRow((CartesianChart)chart6_6, 1);
                        Grid.SetColumn((CartesianChart)chart6_6, 2);
                    }

                    break;
                case eViewMode.Seven:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart7_1 = GetChartBox(1);
                    if (chart7_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_1);
                        Grid.SetRow((PieChart)chart7_1, 0);
                        Grid.SetColumn((PieChart)chart7_1, 0);
                        Grid.SetColumnSpan((PieChart)chart7_1, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_1);
                        Grid.SetRow((CartesianChart)chart7_1, 0);
                        Grid.SetColumn((CartesianChart)chart7_1, 0);
                        Grid.SetColumnSpan((CartesianChart)chart7_1, 2);
                    }


                    var chart7_2 = GetChartBox(2);
                    if (chart7_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_2);
                        Grid.SetRow((PieChart)chart7_2, 0);
                        Grid.SetColumn((PieChart)chart7_2, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_2);
                        Grid.SetRow((CartesianChart)chart7_2, 0);
                        Grid.SetColumn((CartesianChart)chart7_2, 2);
                    }


                    var chart7_3 = GetChartBox(3);
                    if (chart7_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_3);
                        Grid.SetRow((PieChart)chart7_3, 0);
                        Grid.SetColumn((PieChart)chart7_3, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_3);
                        Grid.SetRow((CartesianChart)chart7_3, 0);
                        Grid.SetColumn((CartesianChart)chart7_3, 3);
                    }


                    var chart7_4 = GetChartBox(4);
                    if (chart7_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_4);
                        Grid.SetRow((PieChart)chart7_4, 1);
                        Grid.SetColumn((PieChart)chart7_4, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_4);
                        Grid.SetRow((CartesianChart)chart7_4, 1);
                        Grid.SetColumn((CartesianChart)chart7_4, 0);
                    }


                    var chart7_5 = GetChartBox(5);
                    if (chart7_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_5);
                        Grid.SetRow((PieChart)chart7_5, 1);
                        Grid.SetColumn((PieChart)chart7_5, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_5);
                        Grid.SetRow((CartesianChart)chart7_5, 1);
                        Grid.SetColumn((CartesianChart)chart7_5, 1);
                    }


                    var chart7_6 = GetChartBox(6);
                    if (chart7_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_6);
                        Grid.SetRow((PieChart)chart7_6, 1);
                        Grid.SetColumn((PieChart)chart7_6, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_6);
                        Grid.SetRow((CartesianChart)chart7_6, 1);
                        Grid.SetColumn((CartesianChart)chart7_6, 2);
                    }


                    var chart7_7 = GetChartBox(7);
                    if (chart7_7 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart7_7);
                        Grid.SetRow((PieChart)chart7_7, 1);
                        Grid.SetColumn((PieChart)chart7_7, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart7_7);
                        Grid.SetRow((CartesianChart)chart7_7, 1);
                        Grid.SetColumn((CartesianChart)chart7_7, 3);
                    }

                    break;
                case eViewMode.Eight:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    var chart8_1 = GetChartBox(1);
                    if (chart8_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_1);
                        Grid.SetRow((PieChart)chart8_1, 0);
                        Grid.SetColumn((PieChart)chart8_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_1);
                        Grid.SetRow((CartesianChart)chart8_1, 0);
                        Grid.SetColumn((CartesianChart)chart8_1, 0);
                    }


                    var chart8_2 = GetChartBox(2);
                    if (chart8_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_2);
                        Grid.SetRow((PieChart)chart8_2, 0);
                        Grid.SetColumn((PieChart)chart8_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_2);
                        Grid.SetRow((CartesianChart)chart8_2, 0);
                        Grid.SetColumn((CartesianChart)chart8_2, 1);
                    }


                    var chart8_3 = GetChartBox(3);
                    if (chart8_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_3);
                        Grid.SetRow((PieChart)chart8_3, 0);
                        Grid.SetColumn((PieChart)chart8_3, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_3);
                        Grid.SetRow((CartesianChart)chart8_3, 0);
                        Grid.SetColumn((CartesianChart)chart8_3, 2);
                    }


                    var chart8_4 = GetChartBox(4);
                    if (chart8_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_4);
                        Grid.SetRow((PieChart)chart8_4, 0);
                        Grid.SetColumn((PieChart)chart8_4, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_4);
                        Grid.SetRow((CartesianChart)chart8_4, 0);
                        Grid.SetColumn((CartesianChart)chart8_4, 3);
                    }


                    var chart8_5 = GetChartBox(5);
                    if (chart8_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_5);
                        Grid.SetRow((PieChart)chart8_5, 1);
                        Grid.SetColumn((PieChart)chart8_5, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_5);
                        Grid.SetRow((CartesianChart)chart8_5, 1);
                        Grid.SetColumn((CartesianChart)chart8_5, 0);
                    }


                    var chart8_6 = GetChartBox(6);
                    if (chart8_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_6);
                        Grid.SetRow((PieChart)chart8_6, 1);
                        Grid.SetColumn((PieChart)chart8_6, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_6);
                        Grid.SetRow((CartesianChart)chart8_6, 1);
                        Grid.SetColumn((CartesianChart)chart8_6, 1);
                    }


                    var chart8_7 = GetChartBox(7);
                    if (chart8_7 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_7);
                        Grid.SetRow((PieChart)chart8_7, 1);
                        Grid.SetColumn((PieChart)chart8_7, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_7);
                        Grid.SetRow((CartesianChart)chart8_7, 1);
                        Grid.SetColumn((CartesianChart)chart8_7, 2);
                    }


                    var chart8_8 = GetChartBox(8);
                    if (chart8_8 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart8_8);
                        Grid.SetRow((PieChart)chart8_8, 1);
                        Grid.SetColumn((PieChart)chart8_8, 3);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart8_8);
                        Grid.SetRow((CartesianChart)chart8_8, 1);
                        Grid.SetColumn((CartesianChart)chart8_8, 3);
                    }

                    break;
                case eViewMode.Night:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    grid.RowDefinitions.Add(row3);

                    var chart9_1 = GetChartBox(1);
                    if (chart9_1 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_1);
                        Grid.SetRow((PieChart)chart9_1, 0);
                        Grid.SetColumn((PieChart)chart9_1, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_1);
                        Grid.SetRow((CartesianChart)chart9_1, 0);
                        Grid.SetColumn((CartesianChart)chart9_1, 0);
                    }


                    var chart9_2 = GetChartBox(2);
                    if (chart9_2 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_2);
                        Grid.SetRow((PieChart)chart9_2, 0);
                        Grid.SetColumn((PieChart)chart9_2, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_2);
                        Grid.SetRow((CartesianChart)chart9_2, 0);
                        Grid.SetColumn((CartesianChart)chart9_2, 1);
                    }


                    var chart9_3 = GetChartBox(3);
                    if (chart9_3 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_3);
                        Grid.SetRow((PieChart)chart9_3, 0);
                        Grid.SetColumn((PieChart)chart9_3, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_3);
                        Grid.SetRow((CartesianChart)chart9_3, 0);
                        Grid.SetColumn((CartesianChart)chart9_3, 2);
                    }


                    var chart9_4 = GetChartBox(4);
                    if (chart9_4 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_4);
                        Grid.SetRow((PieChart)chart9_4, 1);
                        Grid.SetColumn((PieChart)chart9_4, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_4);
                        Grid.SetRow((CartesianChart)chart9_4, 1);
                        Grid.SetColumn((CartesianChart)chart9_4, 0);
                    }


                    var chart9_5 = GetChartBox(5);
                    if (chart9_5 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_5);
                        Grid.SetRow((PieChart)chart9_5, 1);
                        Grid.SetColumn((PieChart)chart9_5, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_5);
                        Grid.SetRow((CartesianChart)chart9_5, 1);
                        Grid.SetColumn((CartesianChart)chart9_5, 1);
                    }


                    var chart9_6 = GetChartBox(6);
                    if (chart9_6 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_6);
                        Grid.SetRow((PieChart)chart9_6, 1);
                        Grid.SetColumn((PieChart)chart9_6, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_6);
                        Grid.SetRow((CartesianChart)chart9_6, 1);
                        Grid.SetColumn((CartesianChart)chart9_6, 2);
                    }


                    var chart9_7 = GetChartBox(7);
                    if (chart9_7 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_7);
                        Grid.SetRow((PieChart)chart9_7, 2);
                        Grid.SetColumn((PieChart)chart9_7, 0);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_7);
                        Grid.SetRow((CartesianChart)chart9_7, 2);
                        Grid.SetColumn((CartesianChart)chart9_7, 0);
                    }


                    var chart9_8 = GetChartBox(8);
                    if (chart9_8 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_8);
                        Grid.SetRow((PieChart)chart9_8, 2);
                        Grid.SetColumn((PieChart)chart9_8, 1);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_8);
                        Grid.SetRow((CartesianChart)chart9_8, 2);
                        Grid.SetColumn((CartesianChart)chart9_8, 1);
                    }


                    var chart9_9 = GetChartBox(9);
                    if (chart9_9 is PieChart)
                    {
                        grid.Children.Add((PieChart)chart9_9);
                        Grid.SetRow((PieChart)chart9_9, 2);
                        Grid.SetColumn((PieChart)chart9_9, 2);
                    }
                    else
                    {
                        grid.Children.Add((CartesianChart)chart9_9);
                        Grid.SetRow((CartesianChart)chart9_9, 2);
                        Grid.SetColumn((CartesianChart)chart9_9, 2);
                    }


                    break;
                default:
                    break;
            }
        }

    }



    #endregion
    public class ChartDic
    {
        public static Dictionary<int, object> mChartDic = new Dictionary<int, object>();
        public static object GetView(int key)
        {
            //CartesianChart 柱状图
            /*
            PieChart 饼状图饼图控件为PieChart，而折线图和柱状图的控件都为CartesianChart。
            虽然前端使用相同的 CartesianChart控件来展示折线图和柱状图，但后端通过创建不同的数据系列对象（LineSeries或 ColumnSeries），
            实现折线图和柱状图，折线图用LineSeries，柱状图用ColumnSeries。
            */
            return mChartDic[key];
        }
        public static Types GetType(int key)
        {
            //CartesianChart 柱状图
            switch (key)
            {
                case 1:
                    return ChartsSetViewModel.Ins.ChartType1;
                case 2:
                    return ChartsSetViewModel.Ins.ChartType2;
                case 3:
                    return ChartsSetViewModel.Ins.ChartType3;
                case 4:
                    return ChartsSetViewModel.Ins.ChartType4;
                case 5:
                    return ChartsSetViewModel.Ins.ChartType5;
                case 6:
                    return ChartsSetViewModel.Ins.ChartType6;
                case 7:
                    return ChartsSetViewModel.Ins.ChartType7;
                case 8:
                    return ChartsSetViewModel.Ins.ChartType8;
                case 9:
                    return ChartsSetViewModel.Ins.ChartType9;
                default:
                    break;
            }
            return Types.柱状图;
        }

    } }


