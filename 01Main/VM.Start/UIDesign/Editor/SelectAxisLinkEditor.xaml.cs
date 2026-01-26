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
    [PropertyEditor(typeof(AxisOperate), "选择轴")]
    public partial class SelectAxisLinkEditor : UserControl
    {
        public SelectAxisLinkEditor()
        {
            this.InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AxisSelectView.Ins.ShowDialog();
            if (AxisSelectViewModel.Ins.ResultLinkData != "")
            {
                PropertyNode propertyNode = base.DataContext as PropertyNode;
                propertyNode.ValueString = AxisSelectViewModel.Ins.ResultLinkData;
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
