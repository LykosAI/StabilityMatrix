using System.Text.Json;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Core.Models.Packages.Config;

public class JsonConfigSharingStrategy : IConfigSharingStrategy
{
    public async Task UpdateAndWriteAsync(
        Stream configStream,
        SharedFolderLayout layout,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        IEnumerable<string> clearPaths,
        ConfigSharingOptions options,
        CancellationToken cancellationToken = default
    )
    {
        JsonObject jsonNode;
        var initialPosition = configStream.Position;
        var isEmpty = configStream.Length - initialPosition == 0;

        if (isEmpty)
        {
            jsonNode = new JsonObject();
        }
        else
        {
            try
            {
                // Ensure we read from the current position, respecting potential BOMs etc.
                jsonNode =
                    await JsonSerializer
                        .DeserializeAsync<JsonObject>(
                            configStream,
                            options.JsonSerializerOptions,
                            cancellationToken
                        )
                        .ConfigureAwait(false) ?? new JsonObject();
            }
            catch (JsonException ex)
            {
                // Handle cases where the file might exist but be invalid JSON
                // Log the error, maybe throw a specific exception or return default
                // For now, we'll treat it as empty/new
                System.Diagnostics.Debug.WriteLine(
                    $"Error deserializing JSON config: {ex.Message}. Treating as new."
                );
                jsonNode = new JsonObject();
                isEmpty = true; // Ensure we overwrite if deserialization failed
            }
        }

        UpdateJsonConfig(layout, jsonNode, pathsSelector, clearPaths, options);

        // Reset stream to original position (or beginning if new/failed) before writing
        configStream.Seek(initialPosition, SeekOrigin.Begin);
        // Truncate the stream in case the new content is shorter
        configStream.SetLength(initialPosition + 0); // Truncate from the original position onwards

        await JsonSerializer
            .SerializeAsync(configStream, jsonNode, options.JsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        await configStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void UpdateJsonConfig(
        SharedFolderLayout layout,
        JsonObject rootNode, // Changed parameter name for clarity
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        IEnumerable<string> clearPaths,
        ConfigSharingOptions options
    )
    {
        var rulesByConfigPath = layout.GetRulesByConfigPath();
        var allRuleConfigPaths = rulesByConfigPath.Keys.ToHashSet(); // For cleanup

        foreach (var (configPath, rule) in rulesByConfigPath)
        {
            var paths = pathsSelector(rule).ToArray();
            var normalizedPaths = paths.Select(p => p.Replace('\\', '/')).ToArray();

            JsonNode? valueNode = null;
            if (normalizedPaths.Length > 1 || options.AlwaysWriteArray)
            {
                valueNode = new JsonArray(
                    normalizedPaths.Select(p => JsonValue.Create(p)).OfType<JsonNode>().ToArray()
                );
            }
            else if (normalizedPaths.Length == 1)
            {
                valueNode = JsonValue.Create(normalizedPaths[0]);
            }

            SetJsonValue(rootNode, configPath, valueNode); // Use helper to set/remove value
        }

        // Optional: Cleanup - Remove keys defined in layout but now empty?
        // This might be complex if paths overlap. Current SetJsonValue(..., null) handles removal.
        // We might need a separate cleanup pass if strictly necessary.
    }

    private static void SetJsonValue(JsonObject root, string dottedPath, JsonNode? value)
    {
        var segments = dottedPath.Split('.');
        JsonObject currentNode = root;

        // Traverse or create nodes up to the parent of the target
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (
                !currentNode.TryGetPropertyValue(segment, out var nextNode)
                || nextNode is not JsonObject nextObj
            )
            {
                // If node doesn't exist or isn't an object, create it (overwriting if necessary)
                nextObj = new JsonObject();
                currentNode[segment] = nextObj;
            }
            currentNode = nextObj;
        }

        var finalSegment = segments[^1]; // Get the last segment (the key name)

        if (value != null)
        {
            // Set or replace the value
            currentNode[finalSegment] = value.DeepClone(); // Use DeepClone to avoid node reuse issues
        }
        else
        {
            // Remove the key if value is null
            currentNode.Remove(finalSegment);
            // Optional: Clean up empty parent nodes recursively if desired (more complex)
        }
    }
}
