// PerfectWorldManager.Gui/Converters/NullToBooleanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace PerfectWorldManager.Gui.Converters
{
    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Returns true if the value is not null, false otherwise.
            // This is useful for IsEnabled properties.
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not typically needed for IsEnabled bindings.
            throw new NotImplementedException();
        }
    }
}