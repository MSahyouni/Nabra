using System;
using System.Globalization;
using System.Windows.Data;

namespace ERPUI.Helpers
{
    /// <summary>
    /// Compares two values and returns true if they are equal (string comparison).
    /// Used with MultiBinding in DataTemplates to highlight selected items.
    /// </summary>
    public class EqualsMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            string val1 = values[0]?.ToString() ?? "";
            string val2 = values[1]?.ToString() ?? "";

            return val1.Equals(val2, StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

