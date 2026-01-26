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
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenCvSharp;
using UserControl = System.Windows.Controls.UserControl;

namespace ImageControl
{
    /// <summary>
    /// OpencvWindows.xaml 的交互逻辑
    /// </summary>
    /// 
    public partial class OpencvWindows : UserControl
    {
        public CvControl control { get; set; }
        //private CvDispObj.CvDispText text = new CvDispObj.CvDispText("123", new CvPoint(200, 200)) { Color= System.Drawing.Color.Green,FontSize=29f };
        //private CvDrawObj.CvDrawRotatedRect _roi = new CvDrawObj.CvDrawRotatedRect(new CvRotatedRect(809.333333333333, 126.666666666667, 2.28267873323278, 1445.59724358774, 100));
        public OpencvWindows()
        {
            InitializeComponent();
            control = CvControl;
            //OpencvHost.Child = new System.Windows.Forms.Control();
            //control = new OpenCvSharp.CvControl();
            //control.Name = "cvControl";
            //control.Dock = DockStyle.Fill; // 设置控件自动填充
            //OpencvHost.Child.Controls.Add(control);

        }
        public void DispImage(Mat image)
        {
            control.FitImage();
            control.DispImage(image);
            
        }
        public void FitWindow()
        {
            control.FitImage();
        }
        public void DispStr(DispTextPara dispTextPara )
        {
            control.FitImage();
            CvDispObj.CvDispText text= new CvDispObj.CvDispText(dispTextPara.Text, dispTextPara.Point) { Color = dispTextPara.Color ,FontSize= dispTextPara.FontSize};
            control.DispObj(text);
        }
        public void Draw(CvDrawObj ROI, bool clear = false)
        {
            
            control.DrawObj(ROI, clear);
        }
        public void DispClear(bool  clear = true,bool clear2=true)
        {
            control.DispClear(clear, clear2);
        }
        public void DispObj(params CvDispObj[] objs)
        {
            control.DispObj(objs);
        }
        public void DispObj(IEnumerable<CvDispObj> objs)
        {
            control.DispObj(objs);
        }




    }
}
