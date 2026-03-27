using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DatasetStudio.Models;

namespace DatasetStudio.Converters;

public sealed class TagStatusToBrushConverter : IValueConverter
{
    public static readonly TagStatusToBrushConverter Instance = new TagStatusToBrushConverter();

    private static readonly IBrush UntaggedBrush = new SolidColorBrush(Color.Parse("#CC241D"));
    private static readonly IBrush AutoTaggedBrush = new SolidColorBrush(Color.Parse("#D79921"));
    private static readonly IBrush ReadyBrush = new SolidColorBrush(Color.Parse("#98971A"));
    private static readonly IBrush FallbackBrush = new SolidColorBrush(Color.Parse("#7C6F64"));

    public TagStatusToBrushConverter()
    {
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TagStatus tagStatus)
        {
            if (tagStatus == TagStatus.Untagged)
            {
                return UntaggedBrush;
            }

            if (tagStatus == TagStatus.AutoTagged)
            {
                return AutoTaggedBrush;
            }

            if (tagStatus == TagStatus.Ready)
            {
                return ReadyBrush;
            }
        }

        return FallbackBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
