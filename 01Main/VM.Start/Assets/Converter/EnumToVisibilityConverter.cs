using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using HV.Common.Enums;
using HV.ViewModels;

namespace HV.Assets.Converter
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            var currentState = value.ToString();
            var targetStates = parameter.ToString().Split(',');

            foreach (var state in targetStates)
            {
                if (currentState.Equals(state.Trim()))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
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
    public class EnumToVisibilityConverterForChart : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            int currentState = (int)((eViewMode)value);
            int targetStates = (int)((eViewMode)parameter);

            if(currentState>= targetStates)
                 return Visibility.Visible;
                
            

            return Visibility.Collapsed;
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
