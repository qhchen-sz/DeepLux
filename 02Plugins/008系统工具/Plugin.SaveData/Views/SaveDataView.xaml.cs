using EventMgrLib;
using Plugin.SaveData.Models;
using Plugin.SaveData.ViewModels;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HV.Common;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System;

namespace Plugin.SaveData.Views
{
    /// <summary>
    /// VarDefineView.xaml 的交互逻辑
    /// </summary>
    public partial class VarSetView : ModuleViewBase
    {
        public VarSetView()
        {
            InitializeComponent();

        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void dg_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;

            // 向上遍历Visual树，直到找到列头或者网格
            while ((dep != null) &&
                !(dep is DataGridCell) &&
                !(dep is DataGridColumnHeader))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep == null)
                return;
            var vm = DataContext as SaveDataViewModel;
            if (vm == null) return;
            var index = dg.SelectedIndex;
            VarSetModel var = dg.SelectedItem as VarSetModel;
            if (var == null) return;
            //如果是普通网格
            if (dep is DataGridCell)
            {
                DataGridCell cell = dep as DataGridCell;
                if (cell != null)
                {
                    if (cell.Column.Header.ToString() == "链接")
                    {
                        if (var.DataType == "换行")
                            return;
                        CommonMethods.GetModuleList(vm.ModuleParam, VarLinkViewModel.Ins.Modules, var.DataType);
                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{vm.ModuleGuid},AddContent");
                    }
                }
            }
        }


    }
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return parameter;
            return Binding.DoNothing;
        }
    }
    public class PathModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (LinkMode)value == LinkMode.Path ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class VariableModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (LinkMode)value == LinkMode.Variable ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
