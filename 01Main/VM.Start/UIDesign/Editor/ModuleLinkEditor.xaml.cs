using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ICSharpCode.WpfDesign.PropertyGrid;
using
    HV.UIDesign.Control;
using HV.UIDesign.Dialog.ViewModels;
using HV.UIDesign.Dialog.Views;

namespace HV.UIDesign.Editor
{
    [PropertyEditor(typeof(ModuleSetButton), "模块路径")]
    public partial class ModuleLinkEditor : UserControl
    {
        public ModuleLinkEditor()
        {
            this.InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ModuleDataSelectViewModel.Ins.GetData("");
            ModuleDataSelectViewModel.Ins.IsModuleSelect = true;
            ModuleDataSelectView.Ins.ShowDialog();
            if (ModuleDataSelectViewModel.Ins.ResultLinkData != "")
            {
                PropertyNode propertyNode = base.DataContext as PropertyNode;
                propertyNode.ValueString = ModuleDataSelectViewModel.Ins.ResultLinkData;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PropertyNode propertyNode = base.DataContext as PropertyNode;
            string valueString = propertyNode.ValueString;
            if (!string.IsNullOrEmpty(valueString))
            {
                string[] array = valueString.Split(new char[] { '.' });
                if (array.Length == 2)
                {
                    this.showText.Text = valueString;
                }
            }
            else
            {
                this.showText.Text = "";
            }
            this.showText.ToolTip = this.showText.Text;
        }
    }
}
