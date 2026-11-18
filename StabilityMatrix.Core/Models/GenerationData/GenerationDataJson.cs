using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models.GenerationData;

internal static partial class GenerationDataJson
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Parse a JSON object, also accepting the NaN and Infinity values Python writes,
    /// and the trailing null characters some tools add. Returns null for anything else.
    /// </summary>
    public static JsonObject? ParseObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.TrimEnd('\0', ' ', '\n', '\r', '\t');
        if (!text.TrimStart().StartsWith('{'))
            return null;

        try
        {
            return JsonNode.Parse(text, documentOptions: DocumentOptions) as JsonObject;
        }
        catch (JsonException)
        {
            try
            {
                return JsonNode.Parse(
                        PythonConstantRegex().Replace(text, "null"),
                        documentOptions: DocumentOptions
                    ) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public static JsonObject? AsObject(JsonNode? node) =>
        node switch
        {
            JsonObject obj => obj,
            JsonValue value when value.TryGetValue<string>(out var text) => ParseObject(text),
            _ => null,
        };

    public static string? GetString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<string>(out var text))
            return text;
        return value.GetValueKind() is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
            ? value.ToJsonString()
            : null;
    }

    public static string? AsText(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => node.ToJsonString(),
        };

    public static double? GetDouble(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (
            value.GetValueKind() == JsonValueKind.Number
            && double.TryParse(
                value.ToJsonString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var n
            )
        )
            return n;
        if (
            value.TryGetValue<string>(out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
        )
            return number;
        return null;
    }

    public static ulong GetSeed(JsonNode? node)
    {
        if (node is not JsonValue value)
            return 0;
        if (value.TryGetValue<ulong>(out var seed))
            return seed;
        var text = value.GetValueKind() == JsonValueKind.Number ? value.ToJsonString() : GetString(value);
        if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            return seed;
        return GetDouble(value) is { } number and >= 0 and < 1.8e19 ? (ulong)number : 0;
    }

    /// <summary>
    /// Get a property of a node that may not be an object
    /// </summary>
    public static JsonNode? Get(JsonNode? node, string key) => node is JsonObject obj ? obj[key] : null;

    [GeneratedRegex(@"(?<=[:\[,]\s*)-?(?:NaN|Infinity)(?=\s*[,\]}])")]
    private static partial Regex PythonConstantRegex();
}
