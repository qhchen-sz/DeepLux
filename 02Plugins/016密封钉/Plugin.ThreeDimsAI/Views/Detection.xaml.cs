using Plugin.ThreeDimsAI.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Plugin.ThreeDimsAI.Views
{
    /// <summary>
    /// Detection.xaml 的交互逻辑
    /// </summary>
    public partial class Detection : UserControl
    {
        public Detection()
        {
            InitializeComponent();
        }

    }
    public class AiClassToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 根据 AiClass 的值决定 Visibility
            // 假设当 AiClass 为某个特定值时隐藏
            if (value is eAiClass aiClass)
            {
                if (value != null && ((eAiClass)value != eAiClass.GPU))
                    return Visibility.Visible;
                else
                    return Visibility.Hidden;
            }
            else if(value is bool)
            {
                if ((bool)value)
                    return "启用";
                else
                    return "未启用";
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AiClassToVisibilityConverter2 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 根据 AiClass 的值决定 Visibility
            // 假设当 AiClass 为某个特定值时隐藏
            if (value is eAiClass aiClass)
            {
                if (value != null && ((eAiClass)value != eAiClass.GPU))
                    return Visibility.Visible;
                else
                    return Visibility.Hidden;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
