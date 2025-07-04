using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PerfectWorldManager.Gui.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b)
            {
                boolValue = b;
            }

            bool invert = false;
            if (parameter != null && bool.TryParse(parameter.ToString(), out bool parsedInvert))
            {
                invert = parsedInvert;
            }
            if (parameter != null && parameter.ToString().ToLower() == "invert") // Simpler string check
            {
                invert = true;
            }


            if (invert)
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool boolValue = (visibility == Visibility.Visible);

                bool invert = false;
                if (parameter != null && bool.TryParse(parameter.ToString(), out bool parsedInvert))
                {
                    invert = parsedInvert;
                }
                if (parameter != null && parameter.ToString().ToLower() == "invert")
                {
                    invert = true;
                }

                if (invert)
                {
                    boolValue = !boolValue;
                }
                return boolValue;
            }
            return false;
        }
    }
}