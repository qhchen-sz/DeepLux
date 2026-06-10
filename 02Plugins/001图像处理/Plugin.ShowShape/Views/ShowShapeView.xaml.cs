using Plugin.ShowShape.ViewModels;
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
using HV.Core;

namespace Plugin.ShowShape.Views
{
    public partial class ShowShapeView : ModuleViewBase
    {
        public ShowShapeView()
        {
            InitializeComponent();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
