using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace RecordingsHelper.WPF.Converters
{
    public class StringToLineCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Where(line => !string.IsNullOrWhiteSpace(line))
                               .Count();
                return lines;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
