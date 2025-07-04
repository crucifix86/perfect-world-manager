// PerfectWorldManager.Gui/Converters/BooleanToRunningStatusConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace PerfectWorldManager.Gui.Converters
{
    public class BooleanToRunningStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return booleanValue ? "Running" : "Stopped";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}