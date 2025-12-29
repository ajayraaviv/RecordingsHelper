using System;
using System.Globalization;
using System.Windows.Data;

namespace RecordingsHelper.WPF.Converters
{
    public class SpeakerOriginalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string originalSpeaker && !string.IsNullOrWhiteSpace(originalSpeaker))
            {
                return $" ({originalSpeaker})";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
