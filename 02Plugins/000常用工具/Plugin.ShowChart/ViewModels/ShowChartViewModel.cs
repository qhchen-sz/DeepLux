using EventMgrLib;
using HalconDotNet;
using Plugin.ShowChart.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views.Dock;
using LiveCharts.Wpf;
using System.Windows.Controls;
using LiveCharts;
using System.Windows.Media;
using System.Windows.Threading;
using Separator = LiveCharts.Wpf.Separator;

namespace Plugin.ShowChart.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        nImageIndex,
        InputParaLink,
        InputAxisXStringLink,
        LineDispCount,
        ColumnName

    }

    #endregion

    [Category("常用工具")]
    [DisplayName("图表显示")]
    [ModuleImageName("ShowChart")]
    [Serializable]
    public class ShowChartViewModel : ModuleBase
    {
        [NonSerialized]
        bool IsParaInit = true;
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {

                var Chart = ChartDic.GetView(DispChartViewID + 1);
                Types type = ChartDic.GetType(DispChartViewID + 1);
                if (Chart is PieChart PieChart)
                {
                    // 获取当前窗口的 Dispatcher
                    if (IsOpenWindows)
                    {
                        var view = ModuleView as ShowChartView;
                        view.ChartGrid.Children.Clear();
                        view.ChartGrid.Children.Add(SetPieChart());
                    }
                    else
                    {
                        Dispatcher currentDispatcher = HV.Views.MainView.Ins.Dispatcher;
                        currentDispatcher.Invoke(() =>
                        {
                            SetPieChartGobol(ref PieChart);
                        });
                    }


                    
                }
                else if(Chart is CartesianChart CartesianChart)
                {
                    if (IsOpenWindows)
                    {
                        var view = ModuleView as ShowChartView;
                        view.ChartGrid.Children.Clear();
                        if(type == Types.折线图)
                            view.ChartGrid.Children.Add(SetCartesianChart(true));
                        else if(type == Types.柱状图)
                            view.ChartGrid.Children.Add(SetCartesianChart(false));
                    }
                    else
                    {
                        Dispatcher currentDispatcher = HV.Views.MainView.Ins.Dispatcher;
                        currentDispatcher.Invoke(() =>
                        {
                            if (type == Types.折线图)
                                SetCartesianChartGobol(ref CartesianChart,true);
                            else if (type == Types.柱状图)
                                SetCartesianChartGobol(ref CartesianChart);

                        });
                        
                    }
                        
                }
                if (!IsOpenWindows)
                    IsParaInit = false;
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }
        private void SetPieChart(PieChart chart)
        {
            // 创建饼图系列
            var pieSeries = new PieSeries
            {
                Title = $"OK1",
                Values = new ChartValues<double> { 300 },
                DataLabels = true,
                LabelPoint = point => $"{point.SeriesView.Title}:{point.Y}  {point.Y / point.Sum * 100}% ",
                FontSize = 12,
                Foreground = Brushes.White,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            var pieSeries2 = new PieSeries
            {
                Title = $"NG1",
                Values = new ChartValues<double> { 100 },
                DataLabels = true,
                LabelPoint = point => $"{point.SeriesView.Title}:{point.Y}  {point.Y / point.Sum * 100}% ",
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

            chart.Series = new SeriesCollection { pieSeries, pieSeries2 };
        }
        public override void AddOutputParams()
        {
            base.AddOutputParams();

        }
        #region Prop
        private LinkVarModel _ColumnName = new LinkVarModel() { Text = "1" };
        public LinkVarModel ColumnName
        {
            get { return _ColumnName; }
            set { _ColumnName = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _LineDispCount = new LinkVarModel() { Text = "50" };
        public LinkVarModel LineDispCount
        {
            get { return _LineDispCount; }
            set { _LineDispCount = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _nImageIndex = new LinkVarModel() { Text = "0" };
        /// <summary>
        /// 图像索引
        /// </summary>
        public LinkVarModel nImageIndex
        {
            get { return _nImageIndex; }
            set { _nImageIndex = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _InputAxisXString = new LinkVarModel() { Value = "A"};
        public LinkVarModel InputAxisXString
        {
            get { return _InputAxisXString; }
            set { _InputAxisXString = value; RaisePropertyChanged(); }
        }

        private ObservableCollection<ImageParams> _ImageParam = new ObservableCollection<ImageParams>();
        /// <summary>
        /// 定义图像参数
        /// </summary>
        public ObservableCollection<ImageParams> ImageParam
        {
            get { return _ImageParam; }
            set { _ImageParam = value; RaisePropertyChanged(); }
        }

        private int _nSelectIndex;
        public int nSelectIndex
        {
            get { return _nSelectIndex; }
            set { Set(ref _nSelectIndex, value); }
        }

        /// <summary>显示结果区域</summary>
        private bool _ShowResultRoi = true;
        public bool ShowResultRoi
        {
            get { return _ShowResultRoi; }
            set { Set(ref _ShowResultRoi, value); }
        }
        private bool _ShowImage = true;
        /// <summary>
        /// 覆盖图像
        /// </summary>
        public bool ShowChart
        {
            get { return _ShowImage; }
            set
            {
                Set(ref _ShowImage, value);
            }
        }
        private bool _ShowOkLog;
        /// <summary>
        /// 显示OK日志
        /// </summary>
        public bool ShowOkLog
        {
            get { return _ShowOkLog; }
            set
            {
                Set(ref _ShowOkLog, value);
            }
        }
        private bool _ShowNgLog;
        /// <summary>
        /// 显示NG日志
        /// </summary>
        public bool ShowNgLog
        {
            get { return _ShowNgLog; }
            set
            {
                Set(ref _ShowNgLog, value);
            }
        }


        public ChartType CurChartType
        {
            get { return _CurChartType; }
            set { _CurChartType = value; RaisePropertyChanged(); }
        }
        private ChartType _CurChartType = ChartType.柱状图;
        public List<string> ChartsList { get; set; } =
    new List<string>()
    {
                "图表窗口1",
                "图表窗口2",
                "图表窗口3",
                "图表窗口4",
                "图表窗口5",
                "图表窗口6",
                "图表窗口7",
                "图表窗口8",
                "图表窗口9",
    };
        private int _DispChartViewID = 0;

        /// <summary>
        /// 窗口ID
        /// </summary>
        public int DispChartViewID
        {
            get { return _DispChartViewID; }
            set
            {
                Set( ref _DispChartViewID,value );
                if (IsOpenWindows)
                {
                    ShowChartWindow();
                }
                    
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            IsParaInit = true;
            var view = ModuleView as ShowChartView;
            ObservableCollection < ImageParams > temp = new ObservableCollection<ImageParams>(ImageParam);
            ImageParam = new ObservableCollection<ImageParams>();
            foreach (ImageParams param in temp)
            {
                ImageParams imageParams = new ImageParams()
                {
                    Index = param.Index,
                    Background = param.Background,
                    InputPara = param.InputPara,
                    LinkCommand = this.LinkCommand,
                    ParaName = param.ParaName,
                };
                ImageParam.Add(imageParams);
            }
            if (view != null)
            {
                ClosedView = true;
                ShowChartWindow();
            }
        }
        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                {
                    _ExecuteCommand = new CommandBase((obj) =>
                    {
                        ExeModule();
                    });
                }
                return _ExecuteCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = this.ModuleView as ShowChartView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "nImageIndex":
                    nImageIndex.Text = obj.LinkName;
                    break;
                case "InputParaLink":
                    ImageParam[nSelectIndex].InputPara.Text = obj.LinkName;
                    break;
                case "InputAxisXStringLink":
                    InputAxisXString.Text = obj.LinkName;
                    break;
                case "LineDispCount":
                    InputAxisXString.Text = obj.LinkName;
                    break;
                case "ColumnName":
                    InputAxisXString.Text = obj.LinkName;
                    break;
                default:
                    break;
            }
        }
        [NonSerialized]
        private CommandBase _LinkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.nImageIndex:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},nImageIndex");
                                break;
                            case eLinkCommand.InputParaLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int,double[],int[]");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputParaLink");
                                break;
                            case eLinkCommand.InputAxisXStringLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputAxisXStringLink");
                                break;
                            case eLinkCommand.LineDispCount:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LineDispCount");
                                break;
                            case eLinkCommand.ColumnName:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},ColumnName");
                                break;
                            default:
                                break;
                        }

                    });
                }
                return _LinkCommand;
            }
        }
        [NonSerialized]
        private CommandBase _DataOperateCommand;
        public CommandBase DataOperateCommand
        {
            get
            {
                if (_DataOperateCommand == null)
                {
                    _DataOperateCommand = new CommandBase((obj) =>
                    {
                        switch (obj)
                        {
                            case "Add":
                                ImageParam.Add(new ImageParams()
                                {
                                    Index = ImageParam.Count,
                                    LinkCommand = LinkCommand,
                                    ParaName ="数值"+ ImageParam.Count,
                                    Background = Brushes.MediumSeaGreen

                    });
                                break;
                            case "Delete":
                                if (nSelectIndex < 0) return;
                                int temp = nSelectIndex;
                                ImageParam.RemoveAt(nSelectIndex);
                                if(temp>= ImageParam.Count())
                                    nSelectIndex = ImageParam.Count() - 1;
                                else
                                    nSelectIndex = temp;
                                break;
                            case "Spilt":


                                break;
                            default:
                                break;
                        }
                    });
                }
                return _DataOperateCommand;
            }
        }
        #endregion
        #region Method

        private void ShowChartWindow()
        {
            var view = ModuleView as ShowChartView;
            view.ChartGrid.Children.Clear();
            view.ChartGrid.RowDefinitions.Clear();
            view.ChartGrid.ColumnDefinitions.Clear();
            var chart1 = ChartView.Ins.GetChartBox(DispChartViewID + 1, false);

            if (chart1 is PieChart)
            {
                view.ChartGrid.Children.Add(DeepCopyPieChart((PieChart)chart1));
                CurChartType = ChartType.饼形图;
            }
            else
            {
                view.ChartGrid.Children.Add(DeepCopyCartesianChart((CartesianChart)chart1));
            }

        }
        private PieChart SetPieChart()
        {
            PieChart pieChart = new PieChart();
            pieChart.Series = new SeriesCollection();
            try
            {
                foreach (ImageParams Param in ImageParam)
                {
                    var ee = GetLinkValue(Param.InputPara);
                    var pieSeries = new PieSeries
                    {
                        Title = Param.ParaName,
                        Values = new ChartValues<double>() { Convert.ToDouble(ee) },
                        DataLabels = true,
                        LabelPoint = point => $"{point.SeriesView.Title}:{point.Y}  {Math.Round(point.Y / point.Sum * 100, 2)}% ",
                        FontSize = 12,
                        Foreground = Brushes.White,
                        Stroke = Brushes.White,
                        StrokeThickness = 1,
                        Fill = Param.Background,
                    };
                    pieChart.Series.Add(pieSeries);
                }
            }
            catch (Exception e)
            {

                throw;
            }
            return pieChart;
        }
        private void SetPieChartGobol(ref PieChart pieChart)
        {
            try
            {
                if (pieChart.Series.Count== ImageParam.Count)
                {
                    for (int i = 0; i < pieChart.Series.Count; i++)
                    {
                        PieSeries pie = pieChart.Series[i] as PieSeries;
                        pie.Title = ImageParam[i].ParaName;
                        pie.Values[0] = 50.0;
                        if (pie.Values.Count == 1)
                            pie.Values[0] = Convert.ToDouble(GetLinkValue(ImageParam[i].InputPara));
                        else
                            pie.Values = new ChartValues<double>() { Convert.ToDouble(GetLinkValue(ImageParam[i].InputPara)) };
                        pie.LabelPoint = point => $"{point.SeriesView.Title}:{point.Y}  {Math.Round(point.Y / point.Sum * 100,2) }% ";
                        pie.Fill = ImageParam[i].Background;
                    }
                }
                else
                {
                    pieChart.Series = new SeriesCollection();
                    foreach (ImageParams Param in ImageParam)
                    {
                        var pieSeries = new PieSeries
                        {
                            Title = Param.ParaName,
                            Values = new ChartValues<double>() { Convert.ToDouble(GetLinkValue(Param.InputPara)) },
                            DataLabels = true,
                            LabelPoint = point => $"{point.SeriesView.Title}:{point.Y}  {Math.Round(point.Y / point.Sum * 100, 2)}% ",
                            FontSize = 12,
                            Foreground = Brushes.White,
                            Stroke = Brushes.White,
                            StrokeThickness = 1,
                            Fill = Param.Background,
                        };
                        pieChart.Series.Add(pieSeries);
                    }
                }

            }
            catch (Exception e)
            {

                throw;
            }
        }

        private CartesianChart SetCartesianChart(bool IsLineChart = false)
        {
            CartesianChart cartesianChart = new CartesianChart();
            cartesianChart.Series = new SeriesCollection();
            cartesianChart.AxisX = new AxesCollection();
            cartesianChart.AxisY = new AxesCollection();
            try
            {
            if (IsLineChart)//折线图
            {
                    Dictionary<string, ChartValues<double>> Pairs = new Dictionary<string, ChartValues<double>>();
                    List<SolidColorBrush> Fills = new List<SolidColorBrush>();
                    foreach (ImageParams Param in ImageParam)
                     {
                        var ee = Convert.ToDouble(GetLinkValue(Param.InputPara));
                        if (Pairs.ContainsKey(Param.ParaName))
                        {
                            Pairs[Param.ParaName].Add(ee);
                        }
                        else
                        {
                            Pairs.Add(Param.ParaName, new ChartValues<double>() { ee });
                            Fills.Add(Param.Background);
                        }
                     }
                    int Index = 0;
                        foreach (var item in Pairs)
                        {
                            var series = new LineSeries
                            {
                                Values = item.Value,
                                Title = item.Key,
                                PointGeometry = DefaultGeometries.Diamond,
                                PointGeometrySize = 10,
                                Fill = Brushes.Transparent,
                                Stroke = Fills[Index],
                                StrokeThickness = 2,
                                DataLabels = true,
                                LabelPoint = point => $"{point.Y:N1}",
                                FontSize = 10,
                                Foreground = Brushes.White,
                            };
                            cartesianChart.Series.Add(series);
                            Index++;
                        }
                    var axisX = new Axis
                    {
                        Labels =  new string[1] {GetLinkValue( ColumnName).ToString()},
                        Separator = new Separator { StrokeThickness = 0 }
                    };
                    cartesianChart.AxisX.Add(axisX);

                    cartesianChart.AxisY.Add(new Axis
                    {
                        Separator = new Separator { StrokeThickness = 0 }
                    });


                }
            else//柱形图
            {
                string[] AxisXStr = GetLinkValue(InputAxisXString).ToString().Split(',');
                Dictionary<string, ChartValues<double>> Pairs = new Dictionary<string, ChartValues<double>>();
                List<SolidColorBrush> Fills = new List<SolidColorBrush>();
                foreach (ImageParams Param in ImageParam)
                {
                    var ee = Convert.ToDouble(GetLinkValue(Param.InputPara));
                    if (Pairs.ContainsKey(Param.ParaName))
                    {
                        Pairs[Param.ParaName].Add(ee);
                    }
                    else
                    {
                        Pairs.Add(Param.ParaName, new ChartValues<double>() { ee });
                        Fills.Add(Param.Background);
                    }
                }
                int Index = 0;
                foreach (var item in Pairs)
                {
                    var series = new ColumnSeries
                    {
                        Values = item.Value,
                        Title = item.Key,
                        DataLabels = true,
                        LabelPoint = point => $"{point.Y:N0}",
                        FontSize = 12,
                        Fill = Fills[Index],
                        Foreground = Brushes.White,
                    };
                    cartesianChart.Series.Add(series);
                    Index++;
                }

                var axisX = new Axis
                {
                    Labels = AxisXStr,
                    Separator = new Separator { StrokeThickness = 0 }
                };
                cartesianChart.AxisX.Add(axisX);

                cartesianChart.AxisY.Add(new Axis
                {
                    Separator = new Separator { StrokeThickness = 0 }
                });
            }



            }
            catch (Exception e)
            {

                throw e;
            }
            return cartesianChart;
        }

        private void SetCartesianChartGobol(ref CartesianChart cartesianChart, bool IsLineChart = false)
        {
            try
            {
                if (IsLineChart)
                {
                    int Count = Convert.ToInt32(GetLinkValue(LineDispCount));
                    string Columnname = GetLinkValue(ColumnName).ToString();
                    Dictionary<string, ChartValues<double>> Pairs = new Dictionary<string, ChartValues<double>>();
                    List<SolidColorBrush> Fills = new List<SolidColorBrush>();
                    foreach (ImageParams Param in ImageParam)
                    {
                        var ee = Convert.ToDouble(GetLinkValue(Param.InputPara));
                        if (Pairs.ContainsKey(Param.ParaName))
                        {
                            Pairs[Param.ParaName].Add(ee);
                        }
                        else
                        {
                            Pairs.Add(Param.ParaName, new ChartValues<double>() { ee });
                            Fills.Add(Param.Background);
                        }
                    }
                    if (cartesianChart.Series.Count == Pairs.Count && !IsParaInit)
                    {
                        int i = 0;
                        foreach (var item in Pairs)
                        {
                            LineSeries line = cartesianChart.Series[i] as LineSeries;

                            for (int j = 0; j < item.Value.Count; j++)
                            {
                                if (line.Values.Count > Count)
                                    line.Values.RemoveAt(0);
                                line.Values.Add(item.Value[j]);
                            }
                            i++;
                        }

                    }
                    else
                    {
                        cartesianChart.Series = new SeriesCollection();
                        int Index = 0;
                        foreach (var item in Pairs)
                        {
                            var series = new LineSeries
                            {
                                Values = item.Value,
                                Title = item.Key,
                                PointGeometry = DefaultGeometries.Diamond,
                                PointGeometrySize = 10,
                                Fill = Brushes.Transparent,
                                Stroke = Fills[Index],
                                StrokeThickness = 2,
                                DataLabels = true,
                                LabelPoint = point => $"{point.Y:N1}",
                                FontSize = 10,
                                Foreground = Brushes.White,
                            };
                            cartesianChart.Series.Add(series);
                            Index++;
                        }
                    }
                    if (!IsParaInit)
                    {
                        List<string> tem = new List<string>(cartesianChart.AxisX[0].Labels);
                        if (tem.Count > Count)
                        {
                            cartesianChart.AxisX[0].Labels.RemoveAt(0);
                        }
                        else
                        {
                            tem.Add(Columnname);
                        }
                        cartesianChart.AxisX[0].Labels = tem;
                    }
                    else
                    {
                        cartesianChart.AxisX[0].Labels = new List<string>() { Columnname };
                    }


                }
                else
                {
                    string[] AxisXStr = GetLinkValue(InputAxisXString).ToString().Split(',');
                    Dictionary<string, ChartValues<double>> Pairs = new Dictionary<string, ChartValues<double>>();
                    List<SolidColorBrush> Fills = new List<SolidColorBrush>();
                    foreach (ImageParams Param in ImageParam)
                        {
                            var ee = Convert.ToDouble(GetLinkValue(Param.InputPara));
                            if (Pairs.ContainsKey(Param.ParaName))
                            {
                                Pairs[Param.ParaName].Add(ee);
                            }
                            else
                            {
                                Pairs.Add(Param.ParaName, new ChartValues<double>() { ee });
                                Fills.Add(Param.Background);
                            }
                        }
                    if (cartesianChart.Series.Count == Pairs.Count)
                    {
                        int i = 0;
                        foreach (var item in Pairs)
                        {
                            ColumnSeries column = cartesianChart.Series[i] as ColumnSeries;
                            if(column.Values.Count == item.Value.Count)
                            {
                                for (int j = 0; j < column.Values.Count; j++)
                                {
                                    column.Values[j] = item.Value[j];
                                }
                            }
                            else
                            {
                                column.Values = item.Value;
                            }
                            column.Title = item.Key;
                            column.Fill = Fills[i];
                            i++;
                        }

                    }
                    else
                    {
                        cartesianChart.Series = new SeriesCollection();
                        int Index = 0;
                        foreach (var item in Pairs)
                        {
                            var series = new ColumnSeries
                            {
                                Values = item.Value,
                                Title = item.Key,
                                DataLabels = true,
                                LabelPoint = point => $"{point.Y:N0}",
                                FontSize = 12,
                                Fill = Fills[Index],
                                Foreground = Brushes.White,
                            };
                            cartesianChart.Series.Add(series);
                            Index++;
                        }
                    }
                    
                    if (cartesianChart.AxisX[0].Labels.Count == AxisXStr.Count())
                    {
                        for (int i = 0; i < cartesianChart.AxisX[0].Labels.Count; i++)
                        {
                            cartesianChart.AxisX[0].Labels[i] = AxisXStr[i];
                        }
                    }
                    else
                    {
                        cartesianChart.AxisX = new AxesCollection();
                        var axisX = new Axis
                        {
                            Labels = AxisXStr,
                            Separator = new Separator { StrokeThickness = 0 }
                        };
                        cartesianChart.AxisX.Add(axisX);
                    }

                }
            }
            catch (Exception e)
            {

                throw e;
            }
        }


        private PieChart DeepCopyPieChart(PieChart obj)
        {
            PieChart pieChart = new PieChart();
            pieChart.Series = new SeriesCollection ();
            try
            {
                foreach (PieSeries originalSeries in obj.Series)
                {
                    var pieSeries = new PieSeries
                    {
                        Title = originalSeries.Title,
                        Values = new ChartValues<double>(originalSeries.Values.Cast<double>()),
                        DataLabels = true,
                        LabelPoint = point => $"{point.SeriesView.Title}:{point.Y}  {Math.Round(point.Y / point.Sum * 100, 2)}% ",
                        FontSize = 12,
                        Foreground = Brushes.White,
                        Stroke = Brushes.White,
                        StrokeThickness = 1,
                        Fill = originalSeries.Fill,
                    };
                    pieChart.Series.Add(pieSeries);
                }
            }
            catch (Exception e)
            {

                throw;
            }
            return pieChart;
        }

        private CartesianChart DeepCopyCartesianChart(CartesianChart obj)
        {
            CartesianChart cartesianChart = new CartesianChart();
            cartesianChart.Series = new SeriesCollection();
            cartesianChart.AxisX = new AxesCollection();
            cartesianChart.AxisY = new AxesCollection();
            try
            {
                foreach (var originalSeries in obj.Series)
                {
                    if (originalSeries is ColumnSeries columnSeries)
                    {
                        CurChartType = ChartType.柱状图;
                        var series = new ColumnSeries
                        {
                            Values = new ChartValues<double>(columnSeries.Values.Cast<double>()),
                            Title = columnSeries.Title,
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y:N0}",
                            FontSize = 12,
                            Fill = columnSeries.Fill,
                            Foreground = Brushes.White,
                        };
                        cartesianChart.Series.Add(series);
                        
                    }
                    else if(originalSeries is LineSeries lineSeries)
                    {
                        CurChartType = ChartType.折线图;
                        var seriesCopy = new LineSeries
                        {
                            Title = lineSeries.Title,
                            Values = new ChartValues<double>(lineSeries.Values.Cast<double>()),
                            PointGeometry = DefaultGeometries.Circle,
                            PointGeometrySize = 10,
                            Stroke = lineSeries.Stroke,
                            StrokeThickness = 2,
                            Fill = Brushes.Transparent,
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y:N1}",
                            FontSize = 10,
                            Foreground = Brushes.White,
                        };
                        cartesianChart.Series.Add(seriesCopy);
                    }


                    
                }
                // 复制坐标轴
                if (obj.AxisX.Count > 0)
                {
                    var axisX = new Axis
                    {
                        Title = obj.AxisX[0].Title,
                        Labels = obj.AxisX[0].Labels?.ToList(),
                        Separator = new Separator { StrokeThickness = 0 }
                    };
                    cartesianChart.AxisX.Add(axisX);
                }

                if (obj.AxisY.Count > 0)
                {
                    var axisY = new Axis
                    {
                        Title = obj.AxisY[0].Title,
                        Labels = obj.AxisY[0].Labels?.ToList(),
                        Separator = new Separator { StrokeThickness = 0 }
                    };
                    cartesianChart.AxisY.Add(axisY);
                }
            }
            catch (Exception e)
            {

                throw;
            }
            return cartesianChart;
        }
        #endregion

    }
    [Serializable]
    public class ImageParams : NotifyPropertyBase
    {
        public int Index { get; set; }
        private LinkVarModel _InputPara = new LinkVarModel();
        public LinkVarModel InputPara
        {
            get { return _InputPara; }
            set { _InputPara = value; RaisePropertyChanged(); }
        }
        private string _ParaName;
        public string ParaName
        {
            get { return _ParaName; }
            set { _ParaName = value; RaisePropertyChanged(); }
        }
        [NonSerialized]
        private SolidColorBrush _background;
        public SolidColorBrush Background
        {
            get
            {
                if (_background == null)
                {
                    // 从ARGB值重建画刷
                    _background = new SolidColorBrush(Color.FromArgb(
                        _serializableColor.A,
                        _serializableColor.R,
                        _serializableColor.G,
                        _serializableColor.B));
                }
                return _background;
            }
            set
            {
                if (value != null)
                {
                    // 保存ARGB值
                    var color = value.Color;
                    _serializableColor = new SerializableColor
                    {
                        A = color.A,
                        R = color.R,
                        G = color.G,
                        B = color.B
                    };
                    _background = value;
                }
                RaisePropertyChanged();
            }
        }

        // 在类中使用：
        private SerializableColor _serializableColor;
        public SerializableColor SerializableBackgroundColor
        {
            get { return _serializableColor; }
            set
            {
                _serializableColor = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Background));
            }
        }


        public CommandBase LinkCommand { get; set; }

        private CommandBase _SelectColor;
        public CommandBase SelectColor
        {
            get
            {
                if (_SelectColor == null)
                {
                    _SelectColor = new CommandBase((obj) =>
                    {
                        // 创建并打开颜色选择对话框
                        ColorDialog colorDialog = new ColorDialog();
                        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            // 获取选择的颜色
                            System.Drawing.Color color = colorDialog.Color;
                            // 将 System.Drawing.Color 转换为 System.Windows.Media.Color
                            System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                            // 应用颜色到 Label 的背景
                            Background = new System.Windows.Media.SolidColorBrush(mediaColor);
                        }
                    });
                }
                return _SelectColor;
            }
        }
    }
    [Serializable]
    public struct SerializableColor
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }
}
