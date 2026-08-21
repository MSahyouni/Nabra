using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace Nabrh.Bootstrapper.Helpers
{
    /// <summary>true => Collapsed, false => Visible. (Inverse of the built-in BooleanToVisibilityConverter.)</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v != Visibility.Visible;
    }

    public sealed class BoolToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOk = value is bool b && b;
            return isOk ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.DismissCircle24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class BoolToStatusBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Green = new(Color.FromRgb(0x2E, 0xCC, 0x71));
        private static readonly SolidColorBrush Red = new(Color.FromRgb(0xE7, 0x4C, 0x3C));

        static BoolToStatusBrushConverter()
        {
            Green.Freeze();
            Red.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOk = value is bool b && b;
            return isOk ? Green : Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class RailBadgeBackgroundConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush Gold = new(Color.FromRgb(0xC9, 0xA8, 0x6A));
        private static readonly SolidColorBrush InactiveBg = new(Color.FromRgb(0x17, 0x3B, 0x34));

        static RailBadgeBackgroundConverter()
        {
            Gold.Freeze();
            InactiveBg.Freeze();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = values.Length > 0 && values[0] is bool a && a;
            bool isDone = values.Length > 1 && values[1] is bool d && d;
            return (isActive || isDone) ? Gold : InactiveBg;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public sealed class RailBadgeForegroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveDark = new(Color.FromRgb(0x04, 0x26, 0x23));
        private static readonly SolidColorBrush InactiveText = new(Color.FromRgb(0xCE, 0xDE, 0xDA));

        static RailBadgeForegroundConverter()
        {
            ActiveDark.Freeze();
            InactiveText.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is bool b && b;
            return isActive ? ActiveDark : InactiveText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class PrerequisiteSymbolConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOk = values.Length > 0 && values[0] is bool ok && ok;
            bool isBlocking = values.Length > 1 && values[1] is bool b && b;

            if (isOk) return SymbolRegular.CheckmarkCircle24;
            return isBlocking ? SymbolRegular.DismissCircle24 : SymbolRegular.Info24;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public sealed class PrerequisiteBrushConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush Green = new(Color.FromRgb(0x2E, 0xCC, 0x71));
        private static readonly SolidColorBrush Red = new(Color.FromRgb(0xE7, 0x4C, 0x3C));
        private static readonly SolidColorBrush Orange = new(Color.FromRgb(0xF3, 0x9C, 0x12));

        static PrerequisiteBrushConverter()
        {
            Green.Freeze();
            Red.Freeze();
            Orange.Freeze();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOk = values.Length > 0 && values[0] is bool ok && ok;
            bool isBlocking = values.Length > 1 && values[1] is bool b && b;

            if (isOk) return Green;
            return isBlocking ? Red : Orange;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public sealed class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string text && !string.IsNullOrWhiteSpace(text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class ActivityBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Normal = new(Color.FromRgb(0x5F, 0x6F, 0x6B));
        private static readonly SolidColorBrush Error = new(Color.FromRgb(0xE7, 0x4C, 0x3C));

        static ActivityBrushConverter()
        {
            Normal.Freeze();
            Error.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool isError && isError ? Error : Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
