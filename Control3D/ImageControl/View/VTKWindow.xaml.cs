using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HalconDotNet;
using OpenCvSharp;

namespace ImageControl
{
    /// <summary>
    /// VTKWindow.xaml 的交互逻辑
    /// </summary>
    public partial class VTKWindow : System.Windows.Window
    {
        public VTKWindow()
        {
            InitializeComponent();
/*            var menu = new System.Windows.Controls.ContextMenu();
            var fitItem = new System.Windows.Controls.MenuItem { Header = "适应窗口" };
            fitItem.Click += (s, e) => DispImageFitWindow();
            menu.Items.Add(fitItem);

            vtkControl.ContextMenu = menu;*/
        }

        // 提供一个对外方法用于数据注入
        public void ShowPointCloud(Mat mat)
        {
            vtkControl.CreatePointCloudFromTiffFast(mat);
            vtkControl.vtkRenderWindow2.Is3DVisibleChild = true;
        }

        public void ShowPointCloudHalcon(HObject img)
        {
            //vtkControl.CreatePointCloudFromTiffFastHalcon_Fast(img);
            vtkControl.CreatePointCloudFromTiffFastHalcon_AutoScale(img);
            vtkControl.vtkRenderWindow2.Is3DVisibleChild = true;
        }

        /*        public void DispImageFitWindow()
                {
                    try
                    {
                        *//*                this.WindowH.ResetWindowImage(false);
                                        PaintCross();*//*
                        System.Windows.MessageBox.Show("hahaha");
                    }
                    catch (Exception) { }
                }*/
        /*        private void FitWindowMenuItem_Click(object sender, RoutedEventArgs e)
                {
                    DispImageFitWindow();
                }*/
    }
}
