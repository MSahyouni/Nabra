using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace ERPUI.Helpers
{
    public class BooleanToSymbolConverter : IValueConverter
    {
        public SymbolRegular TrueSymbol { get; set; } = SymbolRegular.Checkmark24;
        public SymbolRegular FalseSymbol { get; set; } = SymbolRegular.Dismiss24;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? TrueSymbol : FalseSymbol;
            return FalseSymbol;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

