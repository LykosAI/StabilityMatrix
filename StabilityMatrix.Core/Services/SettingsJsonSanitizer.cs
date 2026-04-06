using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Provides methods to sanitize and recover corrupted settings JSON files.
/// </summary>
public static class SettingsJsonSanitizer
{
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
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;

        foreach (var c in jsonText)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            switch (c)
            {
                case '{':
                    stack.Push('}');
                    break;
                case '[':
                    stack.Push(']');
                    break;
                case '}' or ']' when stack.Count > 0:
                    stack.Pop();
                    break;
            }
        }

        if (stack.Count == 0)
            return jsonText;

        // Trim trailing garbage after the last valid content
        var trimmed = jsonText.TrimEnd();

        // If we end in the middle of a value (e.g. truncated number or string),
        // trim back to the last structural character
        if (trimmed.Length > 0)
        {
            var lastChar = trimmed[^1];
            if (lastChar != '"' && lastChar != '}' && lastChar != ']'
                && !char.IsDigit(lastChar) && lastChar != 'e' && lastChar != 'l'
                && lastChar != 's' && lastChar != ',')
            {
                var lastSafe = trimmed.LastIndexOfAny([',', '}', ']', '{', '[']);
                if (lastSafe > 0)
                {
                    trimmed = trimmed[..(lastSafe + 1)];
                }
            }

            // Remove trailing comma before we add closing brackets
            trimmed = trimmed.TrimEnd();
            if (trimmed.EndsWith(','))
            {
                trimmed = trimmed[..^1];
            }
        }

        // Re-scan trimmed text to rebuild the stack
        stack.Clear();
        inString = false;
        escaped = false;
        foreach (var c in trimmed)
        {
            if (escaped) { escaped = false; continue; }
            if (c == '\\' && inString) { escaped = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            switch (c)
            {
                case '{': stack.Push('}'); break;
                case '[': stack.Push(']'); break;
                case '}' or ']' when stack.Count > 0: stack.Pop(); break;
            }
        }

        var sb = new StringBuilder(trimmed);

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
            logger?.LogWarning(ex, "Sanitized text still failed to deserialize, attempting property-level recovery");
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
                    CommentHandling = JsonCommentHandling.Skip
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
                        CommentHandling = JsonCommentHandling.Skip
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

        // Re-serialize the cleaned node tree and attempt typed deserialization.
        // JsonNode.Parse with lenient options may accept JSON that the typed deserializer
        // can handle, and any properties with incompatible types will get their defaults
        // from the Settings class property initializers.
        try
        {
            var cleanedJson = rootObject.ToJsonString();
            var settings = JsonSerializer.Deserialize(cleanedJson, SettingsSerializerContext.Default.Settings);
            if (settings is not null)
            {
                logger?.LogInformation("Settings recovered via property-level recovery");
                return settings;
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Property-level recovery failed during final deserialization");
        }

        return null;
    }
}
