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

namespace ImageControl
{
    /// <summary>
    /// Rectangle1Roi.xaml 的交互逻辑
    /// </summary>
    public partial class Rectangle1Roi : UserControl
    {
        private bool isDragging = false;
        private Point mouseStartPosition;
        private Point ellipseStartPosition;
        public Rectangle1Roi()
        {
            InitializeComponent();
        }
        private void Ellipse_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var ellipse = sender as Ellipse;
                if (ellipse != null)
                {
                    var currentPosition = e.GetPosition(this);
                    var offsetX = currentPosition.X - mouseStartPosition.X;
                    var offsetY = currentPosition.Y - mouseStartPosition.Y;

                    // 计算新的中心位置
                    double newCenterX = ellipseStartPosition.X + offsetX + 5; // 加5是因为Ellipse的宽度是10
                    double newCenterY = ellipseStartPosition.Y + offsetY + 5;

                    // 获取当前的ROIRectangle1实例
                    var roi = ellipse.DataContext as ROIRectangle1;
                    if (roi != null)
                    {
                        // 更新ROI的位置
                        roi.moveByHandle(newCenterX, newCenterY);
                    }
                }
            }
        }
        private void Ellipse_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            mouseStartPosition = e.GetPosition(this);
            var ellipse = sender as Ellipse;
            if (ellipse != null)
            {
                ellipseStartPosition = new Point(Canvas.GetLeft(ellipse), Canvas.GetTop(ellipse));
            }
            ellipse.CaptureMouse(); // 开始捕获鼠标
        }

        private void Ellipse_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            var ellipse = sender as Ellipse;
            if (ellipse != null)
            {
                ellipse.ReleaseMouseCapture(); // 停止捕获鼠标
            }
        }
    }
}
