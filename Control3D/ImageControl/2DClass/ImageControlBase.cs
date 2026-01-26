using GalaSoft.MvvmLight;
using OpenCvSharp;
using System.Drawing;

namespace ImageControl
{
    public struct DispTextPara
    {
        public string Text;
        public CvPoint Point;
        public Color Color;
        public float FontSize;
    }
    public enum Status
    {
        OK,
        NG,
        NA
    }
    public class ImageControlBase : ObservableObject
    {

        private Status result = Status.NA;//1:OK  2:NG  3:NA
        private string nGMessage = "无异常";
        public string NGMessage
        {
            get { return nGMessage; }
            set { nGMessage = value; RaisePropertyChanged(() => NGMessage); }
        }
        public Status Result
        {
            get { return result; }
            set { result = value; RaisePropertyChanged(() => Result); }
        }
    }

}
