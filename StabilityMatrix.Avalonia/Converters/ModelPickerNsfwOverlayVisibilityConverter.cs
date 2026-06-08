using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Labs.Controls;

namespace StabilityMatrix.Avalonia.Converters;

public class ModelPickerNsfwOverlayVisibilityConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 4)
            return false;

        // Fail open: if we cannot resolve inputs reliably, do not show the NSFW overlay.
        var showNsfwContent = values[0] as bool?;
        var isModelNsfw = values[1] as bool?;
        var previewPath = values[2] as string;
        var imageState = values[3] as AsyncImageState?;

        var hasImage = !string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath);
        var imageLoaded = imageState == AsyncImageState.Loaded;

        return showNsfwContent == false && isModelNsfw == true && hasImage && imageLoaded;
    }
}
