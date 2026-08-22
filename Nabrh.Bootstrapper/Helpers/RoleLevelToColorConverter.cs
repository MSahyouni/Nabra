using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ERPUI.Helpers
{
    public class RoleLevelToColorConverter : IValueConverter
    {
        public SolidColorBrush GoldBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)); // Figma: #d97706
        public SolidColorBrush SilverBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)); // Figma: #6b7280
        public SolidColorBrush BronzeBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)); // Figma: #2563eb
        public SolidColorBrush GreenBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x04, 0x26, 0x23)); // Figma: #042623

        // Backgrounds
        public SolidColorBrush GoldBg { get; set; } = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB)); // Figma: #fffbeb
        public SolidColorBrush SilverBg { get; set; } = new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB)); // Figma: #f9fafb
        public SolidColorBrush BronzeBg { get; set; } = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF)); // Figma: #eff6ff
        public SolidColorBrush GreenBg { get; set; } = new SolidColorBrush(Color.FromRgb(0xF0, 0xF7, 0xF4)); // Figma: #f0f7f4

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string level = value?.ToString() ?? string.Empty;
            bool isBackground = parameter?.ToString() == "Bg";

            return level switch
            {
                "Gold" => isBackground ? GoldBg : GoldBrush,
                "Silver" => isBackground ? SilverBg : SilverBrush,
                "Bronze" => isBackground ? BronzeBg : BronzeBrush,
                "Green" => isBackground ? GreenBg : GreenBrush,
                _ => isBackground ? SilverBg : SilverBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

