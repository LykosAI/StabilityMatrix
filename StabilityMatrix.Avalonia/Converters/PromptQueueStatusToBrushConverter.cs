using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Maps a <see cref="PromptQueueItemStatus"/> to a status-badge brush. Colors mirror the
/// Theme*Color values in Styles/ThemeColors.axaml.
/// </summary>
public class PromptQueueStatusToBrushConverter : IValueConverter
{
    private static readonly IBrush Grey = new SolidColorBrush(Color.Parse("#9E9E9E"));
    private static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#2196F3"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#4BA04F"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#F44336"));
    private static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#FFC107"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            PromptQueueItemStatus.Pending => Grey,
            PromptQueueItemStatus.Running => Blue,
            PromptQueueItemStatus.Completed => Green,
            PromptQueueItemStatus.Failed => Red,
            PromptQueueItemStatus.Cancelled => Yellow,
            _ => Grey,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
