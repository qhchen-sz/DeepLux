using System;
using System.Globalization;
using System.Windows.Data;
using HV.Common.Enums;

namespace Plugin.LidWeldDetection.ViewModels
{
    public class EnumDescConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum e)
                return REnum.EnumToStr(e);
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
