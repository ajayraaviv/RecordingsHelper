using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RecordingsHelper.WPF.Converters;

public static class TextBlockHighlighter
{
    private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0));

    static TextBlockHighlighter()
    {
        HighlightBrush.Freeze(); // Freeze for better performance
    }

    public static readonly DependencyProperty HighlightTextProperty =
        DependencyProperty.RegisterAttached(
            "HighlightText",
            typeof(string),
            typeof(TextBlockHighlighter),
            new PropertyMetadata(string.Empty, OnHighlightTextChanged));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.RegisterAttached(
            "SearchText",
            typeof(string),
            typeof(TextBlockHighlighter),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

    public static string GetHighlightText(DependencyObject obj) => (string)obj.GetValue(HighlightTextProperty);
    public static void SetHighlightText(DependencyObject obj, string value) => obj.SetValue(HighlightTextProperty, value);

    public static string GetSearchText(DependencyObject obj) => (string)obj.GetValue(SearchTextProperty);
    public static void SetSearchText(DependencyObject obj, string value) => obj.SetValue(SearchTextProperty, value);

    private static void OnHighlightTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
            UpdateHighlighting(textBlock);
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
            UpdateHighlighting(textBlock);
    }

    private static void UpdateHighlighting(TextBlock textBlock)
    {
        var text = GetHighlightText(textBlock);
        var searchText = GetSearchText(textBlock);

        textBlock.Inlines.Clear();

        if (string.IsNullOrEmpty(text))
            return;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            textBlock.Inlines.Add(new Run(text));
            return;
        }

        var lastIndex = 0;
        var comparisonType = StringComparison.OrdinalIgnoreCase;
        var searchLength = searchText.Length;

        while (lastIndex < text.Length)
        {
            var index = text.IndexOf(searchText, lastIndex, comparisonType);
            if (index == -1)
            {
                // Add remaining text
                if (lastIndex < text.Length)
                    textBlock.Inlines.Add(new Run(text.Substring(lastIndex)));
                break;
            }

            // Add text before match
            if (index > lastIndex)
                textBlock.Inlines.Add(new Run(text.Substring(lastIndex, index - lastIndex)));

            // Add highlighted match
            textBlock.Inlines.Add(new Run(text.Substring(index, searchLength))
            {
                Background = HighlightBrush,
                FontWeight = FontWeights.Bold
            });

            lastIndex = index + searchLength;
        }
    }
}
