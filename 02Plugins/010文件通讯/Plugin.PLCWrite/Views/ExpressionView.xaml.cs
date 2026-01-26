using ICSharpCode.CodeCompletion;
using MahApps.Metro.Controls;
using Plugin.PLCWrite.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HV.Common.Enums;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Models;
using HV.Script;
using HV.ViewModels;
using HV.Views;

namespace Plugin.PLCWrite.Views
{
    /// <summary>
    /// GrabImageModuleView.xaml 的交互逻辑
    /// </summary>
    public partial class ExpressionView : MetroWindow
    {
        public ExpressionViewModel viewModel = null;
        public ExpressionView()
        {
            InitializeComponent();
            if (viewModel == null)
            {
                viewModel = new ExpressionViewModel();
            }
            this.DataContext = viewModel;
            winFormHost.Child = viewModel.MyEditer;
            string str = "\r\n\r\n'int表达式 参考格式:\r\nGetInt(\"模块名1.变量名1\") + 123\r\nGetInt(\"模块名1.变量名1\") + GetInt(\"模块名2.变量名2\")\r\nGetInt(\"模块名1.变量名1[0]\")\r\nGetInt(\"模块名1.变量名1[模块名2.变量名2]\")\r\n";
            tip.IsReadOnly = true;
            tip.FontFamily = new FontFamily("Consolas");
            tip.FontSize = 12;
            tip.Completion = new CSharpCompletion(new ScriptProvider(), ScriptProvider.GetRelativeAssemblies()); 
            tip.SetCsharpText(str);
        }
        private VarModel _Var;

        public VarModel Var
        {
            get
            {
                if (_Var == null)
                {
                    _Var = new VarModel();
                }
                return _Var;
            }
            set { _Var = value; }
        }

        public bool IsClosed { get; set; } = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;  // cancels the window close
            IsClosed = true;
            this.Hide();      // Programmatically hides the window
        }

        private void dg_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DataGrid dg = sender as DataGrid;
            if (dg.SelectedItem == null) return;

            var selectModel = tcModuleList.SelectedItem as ModuleList;  //获取到模块名称
            var selectedRow = dg.SelectedItem as VarModel; //获取到变量名称
            string tempStr = "";

            switch (selectedRow.DataType)
            {
                case "bool":
                    tempStr = "GetBool(\"" + "&" + selectModel.DisplayName + "." + selectedRow.Name + "\")";
                    break;
                case "string":
                    tempStr = "GetString(\"" + "&" + selectModel.DisplayName + "." + selectedRow.Name + "\")";
                    break;
                case "double":
                    tempStr = "GetDouble(\"" + "&" + selectModel.DisplayName + "." + selectedRow.Name + "\")";
                    break;
                case "int":
                    tempStr = "GetInt(\"" + "&" + selectModel.DisplayName + "." + selectedRow.Name + "\")";
                    break;
                default:
                    break;
            }
            viewModel.MyEditer.InsertText(viewModel.MyEditer.CurrentPosition, tempStr);
            viewModel.MyEditer.CurrentPosition = viewModel.MyEditer.CurrentPosition + tempStr.Length;
        }

        private void btnCheck_Click(object sender, RoutedEventArgs e)
        {
            CheckCompile(0);
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Var.IsCompileSuccess = false;
            this.Close();
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            CheckCompile(1);
            if (Var.IsCompileSuccess == false) return;
            this.Close();
        }
        /// <summary>
        /// 编译运行脚本
        /// </summary>
        /// <param name="type">区别运行之后是否弹窗 0检查 1确定</param>
        private void CheckCompile(int type)
        {
            Var.m_TempScriptSupport.Source = ExpressionScriptTemplate.GetScriptCode(
                viewModel.m_Param.ProjectID,
                viewModel.m_Param.ModuleName,
                viewModel.MyEditer.Text);
            if (!Var.m_TempScriptSupport.Compile())
            {
                Var.IsCompileSuccess = false;
                MessageView.Ins.MessageBoxShow("编译失败！错误信息：" + Var.m_TempScriptSupport.ErrorText, eMsgType.Error);
            }
            else
            {
                Var.IsCompileSuccess = true;
                var b = Var.m_TempScriptSupport.CodeRun();
                switch (Var.DataType)
                {
                    case "int":
                        if(!(b is int))
                        {
                            MessageView.Ins.MessageBoxShow("编译格式失败：" + b.ToString(), eMsgType.Error);
                            return;
                        }

                        break;
                    case "double":
                        if (!(b is double || b is int))
                        {
                            MessageView.Ins.MessageBoxShow("编译格式失败：" + b.ToString(), eMsgType.Error);
                            return;
                        }

                        break;
                    case "string":
                        break;
                    case "bool":
                        if (!(b is bool))
                        {
                            MessageView.Ins.MessageBoxShow("编译格式失败：" + b.ToString(), eMsgType.Error);
                            return;
                        }
                            
                        break;
                    default:
                        break;
                }
                //var b = Var.m_TempScriptSupport.CodeRun();
                if (type==1)
                {
                    this.Close();
                    return;
                }
                MessageView.Ins.MessageBoxShow(b.ToString());
            }
        }
    }
}
