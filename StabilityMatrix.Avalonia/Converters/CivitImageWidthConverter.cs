using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

[Localizable(false)]
public class CivitImageWidthConverter : IValueConverter
{
    private const string CivitaiHost = "image.civitai.com";
    private const string WidthKey = "width";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // --- Input Validation ---
        Uri? inputUri = null;
        if (value is string urlString && Uri.TryCreate(urlString, UriKind.Absolute, out var parsedUri))
        {
            inputUri = parsedUri;
        }
        else if (value is Uri uri)
        {
            inputUri = uri;
        }

        // Check if it's a valid Civitai URL
        if (
            inputUri == null
            || !inputUri.IsAbsoluteUri
            || !inputUri.Host.Equals(CivitaiHost, StringComparison.OrdinalIgnoreCase)
        )
        {
            // Not a valid Civitai URL, return original value or UnsetValue
            return value is Uri ? value : AvaloniaProperty.UnsetValue;
        }

        // Check and parse the ConverterParameter for the target width
        if (
            parameter == null
            || !int.TryParse(
                parameter.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var targetWidth
            )
            || targetWidth <= 0
        )
        {
            // Invalid or missing width parameter, return original URI
            return inputUri;
        }

        // --- URL Modification ---
        try
        {
            var builder = new UriBuilder(inputUri);
            var pathSegments = builder.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (pathSegments.Count < 2) // Need at least /{guid}/{filename} or similar structure
            {
                // Path structure isn't recognized, return original
                return inputUri;
            }

            // Assume the transformation segment is the second to last one
            var transformSegmentIndex = pathSegments.Count - 2;
            var potentialTransformSegment = pathSegments[transformSegmentIndex];

            var transformSegmentFoundAndUpdated = false;
            var parameters = ParseTransformSegment(potentialTransformSegment);

            if (parameters != null) // It looks like a transformation segment
            {
                var widthExists = parameters.ContainsKey(WidthKey);
                parameters[WidthKey] = targetWidth.ToString(CultureInfo.InvariantCulture); // Update or add width

                // Reconstruct the segment
                pathSegments[transformSegmentIndex] = string.Join(
                    ",",
                    parameters.Select(kvp => $"{kvp.Key}={kvp.Value}")
                );
                transformSegmentFoundAndUpdated = true;
            }

            if (!transformSegmentFoundAndUpdated)
            {
                // Transformation segment not found or didn't parse correctly, insert a new width segment
                pathSegments.Insert(
                    pathSegments.Count - 1,
                    $"{WidthKey}={targetWidth.ToString(CultureInfo.InvariantCulture)}"
                );
            }

            // Reconstruct the path. UriBuilder.Path needs leading '/'
            builder.Path = "/" + string.Join("/", pathSegments);

            // Return the modified Uri
            // Check if the target type prefers string or Uri
            if (targetType == typeof(string))
            {
                return builder.Uri.ToString();
            }
            return builder.Uri;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error converting Civitai URL: {ex.Message}");

            // Return original URI on error
            return inputUri;
        }
    }

    // Simple parser for "key1=value1,key2=value2" format
    private Dictionary<string, string>? ParseTransformSegment(string segment)
    {
        // Basic check: must contain '=' and not be just a filename (heuristic: no '.')
        if (!segment.Contains('=') || segment.Contains('.'))
        {
            return null;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = segment.Split(',');

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2); // Split only on the first '='
            if (keyValue.Length == 2)
            {
                parameters[keyValue[0].Trim()] = keyValue[1].Trim();
            }
            else
            {
                // Invalid pair format, assume segment is not a transform segment
                return null;
            }
        }
        // If we successfully parsed at least one pair, return the dictionary
        return parameters.Count > 0 ? parameters : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(CivitImageWidthConverter)} does not support ConvertBack.");
    }
}
