using ERPUI.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ERPUI.Helpers
{
    public class RoleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role && parameter is string targetRoleStr)
            {
                if (Enum.TryParse<UserRole>(targetRoleStr, out var targetRole))
                {
                    return role == targetRole ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

