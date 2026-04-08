using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Provides methods to sanitize and recover corrupted settings JSON files.
/// </summary>
public static class SettingsJsonSanitizer
{
    private static readonly Dictionary<string, PropertyInfo> SettingsProperties = typeof(Settings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanWrite && property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
        .ToDictionary(GetJsonPropertyName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Strips null bytes (0x00) from raw file content.
    /// </summary>
    public static byte[] SanitizeBytes(byte[] rawBytes)
    {
        // Fast path for clean files
        if (Array.IndexOf(rawBytes, (byte)0x00) < 0)
            return rawBytes;

        return Array.FindAll(rawBytes, b => b != 0x00);
    }

    /// <summary>
    /// Ensures the JSON text has matching brackets by appending missing closing braces/brackets.
    /// Uses a stack to correctly handle nested and mixed <c>{}</c> / <c>[]</c> pairs.
    /// </summary>
    public static string TryFixBraces(string jsonText)
    {
        var normalized = NormalizeClosures(jsonText, out var stack, out _, out _);
        var trimmed = TrimIncompleteValue(normalized);
        var rescanned = NormalizeClosures(trimmed, out stack, out var inString, out var escaped);
        var sb = new StringBuilder(rescanned);

        // If truncated inside a string literal, close it first
        if (inString)
        {
            if (escaped)
                sb.Append('\\');
            sb.Append('"');
        }

        // Append missing closing brackets in correct LIFO order
        while (stack.Count > 0)
        {
            sb.Append('\n').Append(stack.Pop());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Attempts to deserialize settings JSON with progressive recovery strategies.
    /// Returns null if all recovery attempts fail.
    /// </summary>
    public static Settings? TryDeserializeWithRecovery(string jsonText, ILogger? logger = null)
    {
        // Step 1: Sanitize text (strip null bytes, fix braces)
        var sanitized = jsonText.Replace("\0", "");
        sanitized = TryFixBraces(sanitized);

        // Step 2: Try direct deserialization of sanitized text
        try
        {
            var settings = JsonSerializer.Deserialize(sanitized, SettingsSerializerContext.Default.Settings);
            if (settings is not null)
            {
                logger?.LogInformation("Settings recovered after text sanitization");
                return settings;
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(
                ex,
                "Sanitized text still failed to deserialize, attempting property-level recovery"
            );
        }

        // Step 3: Property-level recovery using JsonNode
        return TryPropertyLevelRecovery(sanitized, logger);
    }

    /// <summary>
    /// Attempts to parse JSON with JsonNode, remove corrupt properties, and re-deserialize.
    /// </summary>
    private static Settings? TryPropertyLevelRecovery(string jsonText, ILogger? logger)
    {
        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(
                jsonText,
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }
            );
        }
        catch (JsonException)
        {
            // Try more aggressive cleanup: find the last valid closing brace
            var lastBrace = jsonText.LastIndexOf('}');
            if (lastBrace <= 0)
            {
                logger?.LogWarning("Could not parse JSON even with JsonNode, no recoverable content found");
                return null;
            }

            try
            {
                rootNode = JsonNode.Parse(
                    jsonText[..(lastBrace + 1)],
                    documentOptions: new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip,
                    }
                );
            }
            catch (JsonException)
            {
                logger?.LogWarning("Could not parse JSON even after aggressive cleanup");
                return null;
            }
        }

        if (rootNode is not JsonObject rootObject)
        {
            logger?.LogWarning("Settings JSON root is not an object");
            return null;
        }

        var settings = new Settings();
        var recoveredPropertyCount = 0;

        foreach (var property in rootObject)
        {
            if (property.Value is null)
                continue;

            if (!SettingsProperties.TryGetValue(property.Key, out var targetProperty))
                continue;

            if (!TryDeserializePropertyValue(property.Value, targetProperty.PropertyType, out var value))
            {
                logger?.LogWarning(
                    "Skipping corrupted settings property {PropertyName} during recovery",
                    property.Key
                );
                continue;
            }

            targetProperty.SetValue(settings, value);
            recoveredPropertyCount++;
        }

        logger?.LogInformation(
            "Settings recovered via property-level recovery with {RecoveredPropertyCount} properties",
            recoveredPropertyCount
        );
        return settings;
    }

    private static string NormalizeClosures(
        string jsonText,
        out Stack<char> stack,
        out bool inString,
        out bool escaped
    )
    {
        stack = new Stack<char>();
        inString = false;
        escaped = false;
        var normalized = new StringBuilder(jsonText.Length + 8);

        foreach (var c in jsonText)
        {
            if (escaped)
            {
                normalized.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                normalized.Append(c);
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                normalized.Append(c);
                inString = !inString;
                continue;
            }

            if (inString)
            {
                normalized.Append(c);
                continue;
            }

            switch (c)
            {
                case '{':
                    normalized.Append(c);
                    stack.Push('}');
                    break;
                case '[':
                    normalized.Append(c);
                    stack.Push(']');
                    break;
                case '}'
                or ']':
                    ConsumeClosingToken(c, stack, normalized);
                    break;
                default:
                    normalized.Append(c);
                    break;
            }
        }

        return normalized.ToString();
    }

    private static void ConsumeClosingToken(char token, Stack<char> stack, StringBuilder normalized)
    {
        if (stack.Count == 0)
            return;

        if (stack.Peek() == token)
        {
            normalized.Append(token);
            stack.Pop();
            return;
        }

        if (!stack.Contains(token))
            return;

        while (stack.Count > 0 && stack.Peek() != token)
        {
            normalized.Append(stack.Pop());
        }

        if (stack.Count == 0)
            return;

        normalized.Append(token);
        stack.Pop();
    }

    private static string TrimIncompleteValue(string jsonText)
    {
        var trimmed = jsonText.TrimEnd();
        if (trimmed.Length == 0)
            return trimmed;

        var lastChar = trimmed[^1];
        if (
            lastChar != '"'
            && lastChar != '}'
            && lastChar != ']'
            && !char.IsDigit(lastChar)
            && lastChar != 'e'
            && lastChar != 'l'
            && lastChar != 's'
            && lastChar != ','
        )
        {
            var lastSafe = trimmed.LastIndexOfAny([',', '}', ']', '{', '[']);
            if (lastSafe > 0)
            {
                trimmed = trimmed[..(lastSafe + 1)];
            }
        }

        trimmed = trimmed.TrimEnd();
        if (trimmed.EndsWith(','))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }

    private static string GetJsonPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
    }

    private static bool TryDeserializePropertyValue(JsonNode node, Type propertyType, out object? value)
    {
        try
        {
            value = JsonSerializer.Deserialize(
                node.ToJsonString(),
                propertyType,
                SettingsSerializerContext.Default.Options
            );

            if (value is null && propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null)
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
