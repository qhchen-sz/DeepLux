using Plugin.PLCWrite.ViewModels;
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
using HV.Communacation;
using HV.Core;
using HV.Common;
using HV.Models;

namespace Plugin.PLCWrite.Views
{
    /// <summary>
    /// SaveImageView.xaml 的交互逻辑
    /// </summary>
    public partial class PLCWriteView : ModuleViewBase
    {
        public PLCWriteView()
        {
            InitializeComponent();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void dg_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dg.SelectedItem == null) return;
            VarModel var = dg.SelectedItem as VarModel;
            if (var == null) return;
            var vm = DataContext as PLCWriteViewModel;
            if (vm == null) return;
            if (vm.expressionView == null)
            {
                vm.expressionView = new ExpressionView();
            }

            CommonMethods.GetModuleList(vm.ModuleParam, vm.Modules, var.DataType);
            vm.expressionView.tcModuleList.ItemsSource = null;
            vm.expressionView.tcModuleList.ItemsSource = vm.Modules;
            vm.expressionView.tcModuleList.SelectedIndex = 0;
            vm.expressionView.viewModel.m_Param = vm.ModuleParam;
            if (var.Expression == "NULL")
            {
                vm.expressionView.viewModel.MyEditer.Text = "";
            }
            else
            {
                vm.expressionView.viewModel.MyEditer.Text = var.Expression;
            }
            vm.expressionView.Var = var;
            vm.expressionView.ShowDialog();
            if (vm.expressionView.Var.IsCompileSuccess)
            {
                var.IsCompileSuccess = true;
                var codeRunValue = var.m_TempScriptSupport.CodeRun();
                switch (var.DataType)
                {
                    case "int":
                        if (!(codeRunValue is int))
                        {
                            codeRunValue = 0;
                        }

                        break;
                    case "double":
                        if (!(codeRunValue is double || codeRunValue is int))
                        {
                            codeRunValue = 0;
                        }

                        break;
                    case "string":
                        break;
                    case "bool":
                        if (!(codeRunValue is bool))
                        {
                            codeRunValue = false;
                        }

                        break;
                    default:
                        break;
                }
                var.Value = codeRunValue;
                var.Expression = vm.expressionView.viewModel.MyEditer.Text;
            }
            else
            {
                var.IsCompileSuccess = false;
            }
        }
    }

}
