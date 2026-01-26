using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageControl
{
    [Serializable]
    public class Roi : ObservableObject
    {
        [field: NonSerialized]
        public Func<ImageControl2D, bool, bool> roimouseUpDelegate;
        public virtual void createROI(double midX, double midY) { }
        public virtual void moveByHandle(double x, double y, Roi roi = null) { }
        public virtual double CenterX { get; set; }
        public virtual double CenterY { get; set; }
        public virtual double X { get; set; }
        public virtual double Y { get; set; }
        public virtual double Width { get; set; }
        public virtual double Height { get; set; }
        public double WindowsWidth, WindowsHeight;

    }
}
