// PerfectWorldManager.Gui\Converters\InverseBooleanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace PerfectWorldManager.Gui.Converters // <<< VERY IMPORTANT: Namespace must match
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return false;
        }
    }
}