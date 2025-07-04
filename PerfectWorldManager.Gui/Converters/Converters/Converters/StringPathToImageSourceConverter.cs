// PerfectWorldManager.Gui/Converters/StringPathToImageSourceConverter.cs
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PerfectWorldManager.Gui.Converters
{
    public class StringPathToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    return new BitmapImage(new Uri(path));
                }
                catch
                {
                    // Return a default/placeholder if path is invalid or image fails to load
                    // You might want a specific placeholder image in your assets
                    return null;
                }
            }
            return null; // Or your placeholder
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}