using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Models;
using HV.PersistentData;
using HV.Services;
using HV.Views;
using HV.Views.Dock;
using WPFLocalizeExtension.Engine;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;
using LiveCharts.Defaults;
using HV.ViewModels;

namespace HV.ViewModels
{
    public enum Types
    {
        折线图=0,
        饼图=1,
        柱状图=2
    }
    public class ChartsSetViewModel : NotifyPropertyBase
    {
        #region Singleton

        private static readonly ChartsSetViewModel _instance = new ChartsSetViewModel();

        private ChartsSetViewModel()
        {
            //Init();
        }
        public static ChartsSetViewModel Ins
        {
            get { return _instance; }
        }

        #endregion

        #region Prop
        /// <summary>
        /// 图表1类型
        /// </summary>
        private Types _ChartType1 = Types.柱状图;
        public Types ChartType1
        {
            get { return _ChartType1; }
            set
            {
                _ChartType1 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表2类型
        /// </summary>
        private Types _ChartType2 = Types.柱状图;
        public Types ChartType2
        {
            get { return _ChartType2; }
            set
            {
                _ChartType2 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表3类型
        /// </summary>
        private Types _ChartType3 = Types.柱状图;
        public Types ChartType3
        {
            get { return _ChartType3; }
            set
            {
                _ChartType3 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表4类型
        /// </summary>
        private Types _ChartType4 = Types.柱状图;
        public Types ChartType4
        {
            get { return _ChartType4; }
            set
            {
                _ChartType4 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表5类型
        /// </summary>
        private Types _ChartType5 = Types.柱状图;
        public Types ChartType5
        {
            get { return _ChartType5; }
            set
            {
                _ChartType5 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表6类型
        /// </summary>
        private Types _ChartType6 = Types.柱状图;
        public Types ChartType6
        {
            get { return _ChartType6; }
            set
            {
                _ChartType6 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表7类型
        /// </summary>
        private Types _ChartType7 = Types.柱状图;
        public Types ChartType7
        {
            get { return _ChartType7; }
            set
            {
                _ChartType7 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表8类型
        /// </summary>
        private Types _ChartType8 = Types.柱状图;
        public Types ChartType8
        {
            get { return _ChartType8; }
            set
            {
                _ChartType8 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表9类型
        /// </summary>
        private Types _ChartType9 = Types.柱状图;
        public Types ChartType9
        {
            get { return _ChartType9; }
            set
            {
                _ChartType9 = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Command

        private CommandBase _ActivatedCommand;
        public CommandBase ActivatedCommand
        {
            get
            {
                if (_ActivatedCommand == null)
                {
                    _ActivatedCommand = new CommandBase((obj) =>
                    {
                        if (ChartsSetView.Ins.IsClosed)
                        {
                            ChartsSetView.Ins.IsClosed = false;

                        }

                    });
                }
                return _ActivatedCommand;
            }
        }
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {

                        ChartView.Ins.ViewMode = Solution.Ins.ChartViewMode;
                        ChartsSetView.Ins.Close();
                    });
                }
                return _ConfirmCommand;
            }
        }

        #endregion
        #region 属性
        private SeriesCollection achievement = new SeriesCollection();
        /// <summary>
        /// 成绩柱状图
        /// </summary>
        public SeriesCollection Achievement
        {
            get { return achievement; }
            set { achievement = value; }
        }

        private List<string> studentNameList;
        /// <summary>
        /// 学生名字
        /// </summary>
        public List<string> StudentNameList
        {
            get { return studentNameList; }
            set { studentNameList = value; }
        }

        #endregion


        #region 方法
        /// <summary>
        /// 成绩初始化
        /// </summary>
        //public void Init()
        //{
        //    // 学生姓名列表
        //    StudentNameList = new List<string>()
        //{
        //    "张三", "李四", 
        //};

        //    // 创建多个科目成绩
        //    var subjects = new List<SubjectScore>
        //{
        //    new SubjectScore { Name = "数学", Color = Color.FromRgb(65, 105, 225) }, // 蓝色
        //    new SubjectScore { Name = "语文", Color = Color.FromRgb(220, 20, 60) },   // 红色
        //    new SubjectScore { Name = "英语", Color = Color.FromRgb(50, 205, 50) }    // 绿色
        //};

        //    Random random = new Random();

        //    foreach (var subject in subjects)
        //    {
        //        // 创建柱状图系列
        //        var column = new ColumnSeries
        //        {
        //            Title = subject.Name,
        //            DataLabels = true,
        //            Values = new ChartValues<double>(),
        //            Fill = new SolidColorBrush(subject.Color)
        //        };

        //        // 为每个学生生成随机成绩
        //        for (int i = 0; i < StudentNameList.Count; i++)
        //        {
        //            column.Values.Add((double)(10*(i+1)));
        //        }

        //        // 添加到系列集合
        //        Achievement.Add(column);
        //    }
        //}
    }
    #endregion
}
        // 辅助类


    
    
