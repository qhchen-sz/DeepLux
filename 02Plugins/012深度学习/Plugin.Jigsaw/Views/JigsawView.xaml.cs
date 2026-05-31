using System;
using System.Windows;
using System.Windows.Forms;
using VM.Halcon;
using HV.Core;

namespace Plugin.Jigsaw.Views
{
    public partial class JigsawView : ModuleViewBase
    {
        public JigsawView()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public new VMHWindowControl mWindowH { get; set; }
    }
}