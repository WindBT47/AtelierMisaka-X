using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AtelierMisaka.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool visible && visible;
            bool useCollapsed = string.Equals(parameter as string, "collapse", StringComparison.OrdinalIgnoreCase);

            if (isVisible)
            {
                return Visibility.Visible;
            }

            return useCollapsed ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
