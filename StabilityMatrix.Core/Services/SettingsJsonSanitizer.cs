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
        // Check if any null bytes exist first (fast path for clean files)
        var hasNullBytes = false;
        foreach (var b in rawBytes)
        {
            if (b == 0x00)
            {
                hasNullBytes = true;
                break;
            }
        }

        if (!hasNullBytes)
            return rawBytes;

        // Filter out null bytes
        var result = new byte[rawBytes.Length];
        var writeIndex = 0;
        foreach (var b in rawBytes)
        {
            if (b != 0x00)
            {
                result[writeIndex++] = b;
            }
        }

        return result.AsSpan(0, writeIndex).ToArray();
    }

    /// <summary>
    /// Ensures the JSON text has matching curly braces by appending missing closing braces.
    /// </summary>
    public static string TryFixBraces(string jsonText)
    {
        var openCount = 0;
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
                    openCount++;
                    break;
                case '}':
                    openCount--;
                    break;
            }
        }

        if (openCount > 0)
        {
            // Trim trailing garbage after the last valid content
            var trimmed = jsonText.TrimEnd();

            // If we end in the middle of a value (e.g. truncated number or string),
            // try to find the last complete property by removing the trailing partial content
            if (trimmed.Length > 0)
            {
                var lastChar = trimmed[^1];
                // If the last char isn't a valid JSON value terminator, trim back to the last comma or brace
                if (lastChar != '"' && lastChar != '}' && lastChar != ']'
                    && !char.IsDigit(lastChar) && lastChar != 'e' && lastChar != 'l'
                    && lastChar != 's' && lastChar != ',')
                {
                    // Find the last comma, closing bracket, or opening brace
                    var lastSafe = trimmed.LastIndexOfAny([',', '}', ']', '{']);
                    if (lastSafe > 0)
                    {
                        trimmed = trimmed[..(lastSafe + 1)];
                    }
                }

                // Remove trailing comma before we add closing braces
                trimmed = trimmed.TrimEnd();
                if (trimmed.EndsWith(','))
                {
                    trimmed = trimmed[..^1];
                }
            }

            // Re-count braces after trimming
            openCount = 0;
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
                    case '{': openCount++; break;
                    case '}': openCount--; break;
                }
            }

            // Append missing closing braces
            var sb = new StringBuilder(trimmed);
            for (var i = 0; i < openCount; i++)
            {
                sb.Append('\n').Append('}');
            }

            return sb.ToString();
        }

        return jsonText;
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

        // Remove properties that can't be individually serialized back
        var propsToRemove = new List<string>();
        foreach (var (key, value) in rootObject)
        {
            try
            {
                // Validate each property by serializing it to a string
                _ = value?.ToJsonString();
            }
            catch (Exception)
            {
                propsToRemove.Add(key);
            }
        }

        foreach (var key in propsToRemove)
        {
            logger?.LogWarning("Removing corrupt property from settings: {Key}", key);
            rootObject.Remove(key);
        }

        // Re-serialize the cleaned node tree and attempt typed deserialization
        try
        {
            var cleanedJson = rootObject.ToJsonString();
            var settings = JsonSerializer.Deserialize(cleanedJson, SettingsSerializerContext.Default.Settings);
            if (settings is not null)
            {
                logger?.LogInformation(
                    "Settings recovered via property-level recovery ({RemovedCount} properties removed)",
                    propsToRemove.Count
                );
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
