using System;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace RecordingsHelper.WPF.Converters;

public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            // Smart formatting: show hours only if >= 1 hour
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"hh\:mm\:ss\.fff");
            return timeSpan.ToString(@"mm\:ss\.fff");
        }
        return "00:00.000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
            return TimeSpan.Zero;

        str = str.Trim();

        // Try with hours first (hh:mm:ss.fff)
        if (TimeSpan.TryParseExact(str, @"hh\:mm\:ss\.fff", culture, out var timeSpan))
            return timeSpan;

        // Try hours without milliseconds (hh:mm:ss)
        if (TimeSpan.TryParseExact(str, @"hh\:mm\:ss", culture, out timeSpan))
            return timeSpan;

        // Try exact format (mm:ss.fff)
        if (TimeSpan.TryParseExact(str, @"mm\:ss\.fff", culture, out timeSpan))
            return timeSpan;

        // Try without milliseconds (mm:ss)
        if (TimeSpan.TryParseExact(str, @"mm\:ss", culture, out timeSpan))
            return timeSpan;

        // Try with 2-digit milliseconds (mm:ss.ff)
        if (TimeSpan.TryParseExact(str, @"mm\:ss\.ff", culture, out timeSpan))
            return timeSpan;

        // Try with 1-digit milliseconds (mm:ss.f)
        if (TimeSpan.TryParseExact(str, @"mm\:ss\.f", culture, out timeSpan))
            return timeSpan;

        // Try just seconds as decimal (e.g., "45.5" = 45.5 seconds)
        if (double.TryParse(str, NumberStyles.Float, culture, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        // Try standard TimeSpan parsing as fallback
        if (TimeSpan.TryParse(str, culture, out timeSpan))
            return timeSpan;

        return TimeSpan.Zero;
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool inverse = parameter as string == "Inverse";
            if (inverse)
                boolValue = !boolValue;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        bool inverse = parameter as string == "Inverse";
        
        if (inverse)
            isNull = !isNull;
            
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
