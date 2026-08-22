using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ERPUI.Helpers
{
    public class OutgoingBrushConverter : IValueConverter
    {
        public Brush OutgoingBrush { get; set; } = Brushes.LightBlue;
        public Brush IncomingBrush { get; set; } = Brushes.LightGray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool b && b)
                {
                    if (Application.Current.Resources.Contains("AccentPrimaryBrush"))
                        return Application.Current.Resources["AccentPrimaryBrush"] as Brush ?? OutgoingBrush;
                    if (Application.Current.Resources.Contains("AccentBrush"))
                        return Application.Current.Resources["AccentBrush"] as Brush ?? OutgoingBrush;
                    return OutgoingBrush;
                }

                return IncomingBrush;
            }
            catch
            {
                return IncomingBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}

