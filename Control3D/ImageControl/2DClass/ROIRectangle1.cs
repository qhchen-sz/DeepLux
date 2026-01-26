using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageControl
{
    public class ROIRectangle1 : Roi
    {
        // 矩形ROI的基本属性
        private double x;
        public override double X
        {
            get { return x; }
            set { x = value; RaisePropertyChanged(() => X); }
        }
        private double y;
        public override double Y
        {
            get { return y; }
            set { y = value; RaisePropertyChanged(() => Y); }
        }

        private double width;
        public override double Width
        {
            get { return width; }
            set { width = value; RaisePropertyChanged(() => Width); }
        }
        private double height;
        public override double Height
        {
            get { return height; }
            set { height = value; RaisePropertyChanged(() => Height); }
        }
        private double centerX, centerY;
        public override double CenterX
        {
            get { return centerX; }
            set { centerX = value; RaisePropertyChanged(() => CenterX); }
        }
        public override double CenterY
        {
            get { return centerY; }
            set { centerY = value; RaisePropertyChanged(() => CenterY); }
        }
        //public double CenterX => X + Width / 2;
        //public double CenterY => Y + Height / 2;
        public ROIRectangle1()
        {
            // 初始化默认值
            X = 100;
            Y = 100;
            Width = 100; // 默认宽度
            Height = 100; // 默认高度
            CenterX = X + Width / 2 - 5;
            CenterY = Y + Height / 2 - 5;
        }

        public override void createROI(double midX, double midY)
        {
            // 假设midX, midY是矩形的中心点
            X = midX - Width / 2;
            Y = midY - Height / 2;
            RaisePropertyChanged(() => X);
            RaisePropertyChanged(() => Y);
        }
        public override void moveByHandle(double x, double y, Roi roi = null)
        {
            // 假设x, y是鼠标点击的新位置，这里简单地将ROI移动到新位置
            if( x < 1 || y < 1 || x > WindowsWidth - 1 || y > WindowsHeight - 1 )
                return;
            X = x - Width / 2;
            Y = y - Height / 2;
            CenterX = x - 5;
            CenterY = y - 5;
            RaisePropertyChanged(() => X);
            RaisePropertyChanged(() => Y);
            RaisePropertyChanged(() => CenterX);
            RaisePropertyChanged(() => CenterY);
        }


    }
}
