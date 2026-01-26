using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using HV.Services;
using Plugin.JigsawPuzzle.ViewModels;

namespace Plugin.JigsawPuzzle.Views
{
    /// <summary>
    /// SaveImageView.xaml 的交互逻辑
    /// </summary>
    public partial class JigsawPuzzleView : ModuleViewBase
    {
        public JigsawPuzzleView()
        {
            InitializeComponent();
        
            
        }
        [NonSerialized]
        public VMHWindowControl mWindowH_Template;
        JigsawPuzzleViewModel viewModel;

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

       
    }
}
