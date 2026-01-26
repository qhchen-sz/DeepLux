using HalconDotNet;
using Plugin.Envelope.ViewModels;
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
using System.Globalization;


namespace Plugin.Envelope.Views
{
    /// <summary>
    /// SaveImageView.xaml 的交互逻辑
    /// </summary>
    public partial class EnvelopeView : ModuleViewBase
    {
        public EnvelopeView()
        {
            InitializeComponent();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();

        }

    }
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = (bool)value;
            // 如果传了参数"Inverse"，则反转
            if (parameter != null && parameter.ToString() == "Inverse")
                flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is Visibility vis && vis == Visibility.Visible);
        }
    }
    public class StrToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
           string str = value.ToString();
            bool flag = false;
            // 
            if (parameter != null)
            {
                if (str == "Right")
                {
                    switch (parameter.ToString())
                    {
                        case "Left":
                            flag = true;
                            break;
                        case "Right":
                            flag = false;
                            break;
                        default:
                            flag = false;
                            break;
                    }
                }
                else if(str == "Left")
                {
                    switch (parameter.ToString())
                    {
                        case "Left":
                            flag = false;
                            break;
                        case "Right":
                            flag = true;
                            break;
                        default:
                            flag = false;
                            break;
                    }
                }
                else
                {
                    flag = false;
                }

            }
                flag = !flag;

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is Visibility vis && vis == Visibility.Visible);
        }
        public bool Check(string str)
        {
            bool res = false;
            switch (str)
            {
                case "Left":
                    break;
                case "Right":
                    break;
                default:

                    break;
            }
            return res;
        }
    }
    
}
