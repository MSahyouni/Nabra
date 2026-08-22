using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ERPUI.Helpers
{
    public class ResourceKeyToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string key && Application.Current != null && Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key] as ImageSource;
            }
            
            // If it's a valid URI, return that instead
            if (value is string uriString && uriString.StartsWith("pack://"))
            {
                try
                {
                    return new System.Windows.Media.Imaging.BitmapImage(new Uri(uriString));
                }
                catch { }
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

