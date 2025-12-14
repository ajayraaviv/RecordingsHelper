using System;
using System.Globalization;
using System.Windows.Data;

namespace RecordingsHelper.WPF.Converters
{
    public class ActionToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "Mute";
                
            var enumValue = value.ToString();
            return enumValue == "Remove" ? "Remove" : "Mute";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && str == "Remove")
            {
                return Enum.Parse(targetType, "Remove");
            }
            return Enum.Parse(targetType, "Mute");
        }
    }
}