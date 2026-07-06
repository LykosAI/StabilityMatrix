using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Maps a <see cref="PromptQueueItemStatus"/> to a status-badge brush using the app's
/// Theme*Color resources.
/// </summary>
public class PromptQueueStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            PromptQueueItemStatus.Pending => GetThemeBrush("ThemeGreyColor", Colors.Gray),
            PromptQueueItemStatus.Running => GetThemeBrush("ThemeBlueColor", Colors.DodgerBlue),
            PromptQueueItemStatus.Completed => GetThemeBrush("ThemeGreenColor", Colors.Green),
            PromptQueueItemStatus.Failed => GetThemeBrush("ThemeRedColor", Colors.Firebrick),
            PromptQueueItemStatus.Cancelled => GetThemeBrush("ThemeAmberColor", Colors.Orange),
            _ => GetThemeBrush("ThemeGreyColor", Colors.Gray),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static IBrush GetThemeBrush(string key, Color fallback)
    {
        if (
            Application.Current is { } app
            && app.TryGetResource(key, app.ActualThemeVariant, out var resource)
        )
        {
            switch (resource)
            {
                case IBrush brush:
                    return brush;
                case Color color:
                    return new SolidColorBrush(color);
            }
        }

        return new SolidColorBrush(fallback);
    }
}
