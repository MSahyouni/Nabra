using System;
using System.Globalization;
using System.Windows.Data;

namespace ERPUI.Helpers
{
    public class EnumToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            
            // Basic translation, can be expanded
            return value.ToString() switch
            {
                "SuperAdmin" => "مدير النظام",
                "Admin" => "مدير",
                "DepartmentBoss" => "رئيس قسم",
                "Employee" => "موظف",
                "New" => "جديدة",
                "InProgress" => "قيد التنفيذ",
                "Completed" => "مكتملة",
                "Overdue" => "متأخرة",
                "Low" => "منخفضة",
                "Medium" => "متوسطة",
                "High" => "عالية",
                "Urgent" => "عاجلة",
                _ => value.ToString()!
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

