using System;
using System.Globalization;
using System.Windows; // For double.NaN
using System.Windows.Data;

namespace PerfectWorldManager.Gui.Converters
{
    public class BooleanToHeightConverter : IValueConverter
    {
        public double TrueHeight { get; set; } = 60; // Default height for long text
        public double FalseHeight { get; set; } = double.NaN; // Auto height for short text (or specific like 22)

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLongText)
            {
                return isLongText ? TrueHeight : FalseHeight;
            }
            return FalseHeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}