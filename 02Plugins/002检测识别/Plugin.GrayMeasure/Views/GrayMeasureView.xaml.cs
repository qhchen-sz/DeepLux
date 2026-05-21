using Plugin.GrayMeasure.ViewModels;
using System.Windows;
using VM.Halcon;
using HV.Core;
using HV.Common.Enums;

namespace Plugin.GrayMeasure.ViewModels
{
    public enum eRoiMode
    {
        [EnumDescription("环形ROI")]
        Circle,
        [EnumDescription("圆阵ROI")]
        CircleArray,
        [EnumDescription("矩形阵ROI")]
        RectArray,
    }
}

namespace Plugin.GrayMeasure.Views
{
    public partial class GrayMeasureView : ModuleViewBase
    {
        public GrayMeasureView()
        {
            InitializeComponent();
        }

        private void ModuleViewBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (mWindowH == null)
            {
                mWindowH = new VMHWindowControl();
                winFormHost.Child = mWindowH;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
