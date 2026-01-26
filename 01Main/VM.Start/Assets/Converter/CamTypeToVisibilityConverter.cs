using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace HV.Assets.Converter
{
    public class CamTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            string cameraType = value.ToString();
            string panelType = parameter as string;

            // 当相机类型为3D时
            if (cameraType == "基恩士相机")
            {
                // 参数为"3D"表示这是第二个面板(3D专用)
                if (panelType == "基恩士相机")
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
            // 其他相机类型
            else
            {
                // 参数不为"3D"表示这是第一个面板(非3D相机用)
                if (panelType != "基恩士相机")
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }

            //return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value:bool,RadioButton.IsChecked,value
            // parameter:ConverterParameter,enum,Banana
            if (value is bool b && b)
                return parameter;
            return Binding.DoNothing;
        }
    }
}
