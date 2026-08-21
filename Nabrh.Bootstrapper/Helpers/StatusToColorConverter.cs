using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ERPUI.Helpers
{
    public class StatusToColorConverter : IValueConverter
    {
        public SolidColorBrush GreenBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        public SolidColorBrush RedBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        public SolidColorBrush OrangeBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        public SolidColorBrush BlueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        public SolidColorBrush GoldBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x99, 0x72, 0x1D));
        public SolidColorBrush GrayBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x70, 0x7A, 0x74));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            return status switch
            {
                "مكتملة" or "معتمد" or "طبيعي" or "نشط" or "مغلقة" => GreenBrush,
                "متأخرة" or "مرفوض" or "تصعيد" or "مصعدة" => RedBrush,
                "قيد المعالجة" or "قيد التنفيذ" or "قيد المراجعة" => OrangeBrush,
                "جديدة" => BlueBrush,
                "مسودة" or "معلقة" or "بانتظار الاعتماد" or "مفتوحة" => GoldBrush,
                _ => GrayBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

