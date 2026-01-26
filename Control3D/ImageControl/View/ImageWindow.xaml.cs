using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ImageControl
{
    /// <summary>
    /// ImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImageWindow : UserControl
    {
        public Mat GrayImage;
        public Mat HeightImage;
        private OpencvWindows ImageWindows;
        private VTKControl VTKControl;
        public ImageControlBase imageControlBase = null;
        private bool visual3D = false;
        private bool flag_loadimg = false;
        public ImageControlBase ViewModel
        {
            get { return (ImageControlBase)DataContext; }
            set { DataContext = value; }
        }
        public ImageWindow()
        {
            InitializeComponent();
            imageControlBase = new ImageControlBase();
            this.DataContext = imageControlBase;
            ImageWindows = Image2D;
            VTKControl = Image3D;
            myMeau.OnImageLoaded += MyMeau_OnImageLoaded;
            myMeau.Disp_Button += MyMeau_OnImageShow;
            myMeau.FitWindows += MyMeau_FitImage;
            myMeau.AddLightimg += MyMeau_AddLightimg;

        }
        public void Showimage(Mat mat)
        {
            if (mat != null)
            {
                ImageWindows.DispImage(ConvertTo8Bit(mat));
                Image3D.Visibility = Visibility.Collapsed;
                Image2D.Visibility = Visibility.Visible;
            }
        }
        public void DispText(DispTextPara dispTextPara)
        {
            ImageWindows.DispStr(dispTextPara);
            Image3D.Visibility = Visibility.Collapsed;
            Image2D.Visibility = Visibility.Visible;
        }
        public void SaveCutImage(string path) 
        {
            Application.Current.Dispatcher.Invoke(() => {
                ImageWindows.control.SaveRenderedImage(path);
            }, DispatcherPriority.ContextIdle); // ContextIdle 确保处理完所有操作
            
        }
        private void MyMeau_OnImageLoaded(object sender, string imagePath)
        {
            var image = Cv2.ImRead(imagePath,ImreadModes.Unchanged);
            Showimage(image);
            flag_loadimg = true;


            //    BitmapSource bitmapSource = MatToBitmapSource(image);
            //Image2D.ImageViewer.Source = bitmapSource;

        }
        private void MyMeau_FitImage(object sender, RoutedEventArgs e)
        {
            if(Image2D.Visibility== Visibility.Visible && flag_loadimg)
                ImageWindows.FitWindow();
            if(Image3D.Visibility== Visibility.Visible && flag_loadimg)
                VTKControl.FitWindow();
        }

        private void MyMeau_AddLightimg(object sender, RoutedEventArgs e)
        {
            if (visual3D)
                VTKControl.AddLightimg();
        }

        private void MyMeau_OnImageShow(object sender,bool i)
        {
            if (i)//3D
            {
                if (HeightImage != null && (HeightImage.Type() == MatType.CV_16SC1 || HeightImage.Type()== MatType.CV_32SC1))
                {
                    VTKControl.DispImage(HeightImage);
                    Image3D.Visibility = Visibility.Visible;
                    Image2D.Visibility = Visibility.Collapsed;
                    VTKControl.vtkRenderWindow2.Is3DVisibleChild = true;
                    visual3D = true;
                }



            }
            else
            {
                if (GrayImage != null && (GrayImage.Type() == MatType.CV_8UC1 || GrayImage.Type() == MatType.CV_8UC3))
                {
                    ImageWindows.DispImage(GrayImage);
                    Image3D.Visibility = Visibility.Collapsed;
                    Image2D.Visibility = Visibility.Visible;
                    VTKControl.vtkRenderWindow2.Is3DVisibleChild = false;
                    visual3D = false;
                }

                
            }
        }
        public void DrayRoi(CvDrawObj ROI, bool clear = false)
        {
            ImageWindows.Draw(ROI, clear);
        }
        public Mat ConvertTo8Bit(Mat inputMat)
        {
            // 检查输入的Mat是否为空
            if (inputMat.Empty())
            {
                throw new ArgumentException("Input Mat is empty.");
            }

            // 创建一个新的Mat对象，用于存储转换后的8位图像
            Mat outputMat = new Mat();
            HeightImage = new Mat();
            GrayImage = new Mat();
            // 检查输入图像的深度，进行相应的转换
            if (inputMat.Type() == MatType.CV_16SC1) // 16位有符号
            {
                
                Cv2.CopyTo(inputMat, HeightImage);
                // 将16位图像转换为8位图像
                inputMat.ConvertTo(outputMat, MatType.CV_8UC1, 1.0 / 256);
                outputMat.CopyTo(GrayImage);
            }
            else if (inputMat.Type() == MatType.CV_32SC1 || inputMat.Type() == MatType.CV_32FC1) // 32位有符号
            {
                Cv2.CopyTo(inputMat, HeightImage);
                //HeightImage = inputMat;
                // 将32位图像转换为8位图像
                inputMat.ConvertTo(outputMat, MatType.CV_8UC1, 1.0 / 256); // 注意：这里的系数可能需要根据实际数据调整
                outputMat.CopyTo(GrayImage);
            }
            else if(inputMat.Type() == MatType.CV_8UC1 || inputMat.Type() == MatType.CV_8UC3)
            {
                inputMat.CopyTo(GrayImage);
                return GrayImage;
            }

            return outputMat;
        }
    }
}
