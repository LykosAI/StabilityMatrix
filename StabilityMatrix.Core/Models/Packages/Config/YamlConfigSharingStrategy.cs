using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StabilityMatrix.Core.Models.Packages.Config;

public class YamlConfigSharingStrategy : IConfigSharingStrategy
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
        YamlMappingNode rootNode;
        YamlStream yamlStream = [];
        var initialPosition = configStream.Position;
        var isEmpty = configStream.Length - initialPosition == 0;

        if (!isEmpty)
        {
            try
            {
                using var reader = new StreamReader(configStream, leaveOpen: true);
                yamlStream.Load(reader); // Load existing YAML
                if (
                    yamlStream.Documents.Count > 0
                    && yamlStream.Documents[0].RootNode is YamlMappingNode mapping
                )
                {
                    rootNode = mapping;
                }
                else
                {
                    // File exists but isn't a valid mapping node at the root, start fresh
                    System.Diagnostics.Debug.WriteLine(
                        $"YAML config exists but is not a mapping node. Treating as new."
                    );
                    rootNode = [];
                    yamlStream = new YamlStream(new YamlDocument(rootNode)); // Reset stream content
                    isEmpty = true;
                }
            }
            catch (YamlException ex)
            {
                // Handle cases where the file might exist but be invalid YAML
                System.Diagnostics.Debug.WriteLine(
                    $"Error deserializing YAML config: {ex.Message}. Treating as new."
                );
                rootNode = [];
                yamlStream = new YamlStream(new YamlDocument(rootNode)); // Reset stream content
                isEmpty = true;
            }
        }
        else
        {
            // Stream is empty, create new structure
            rootNode = [];
            yamlStream.Add(new YamlDocument(rootNode));
        }

        UpdateYamlConfig(layout, rootNode, pathsSelector, clearPaths, options);

        // Reset stream to original position (or beginning if new/failed) before writing
        configStream.Seek(initialPosition, SeekOrigin.Begin);
        // Truncate the stream in case the new content is shorter
        configStream.SetLength(initialPosition + 0);

        // Use StreamWriter to write back to the original stream
        // Use default encoding (UTF8 without BOM is common for YAML)
        await using (var writer = new StreamWriter(configStream, leaveOpen: true))
        {
            // Configure serializer for better readability if desired
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance) // Common for ComfyUI paths
                .WithDefaultScalarStyle(ScalarStyle.Literal)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults) // Optional: omit nulls/defaults
                .Build();
            serializer.Serialize(writer, yamlStream.Documents[0].RootNode); // Serialize the modified root node
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false); // Ensure content is written to stream
        }
        await configStream.FlushAsync(cancellationToken).ConfigureAwait(false); // Flush the underlying stream
    }

    private static void UpdateYamlConfig(
        SharedFolderLayout layout,
        YamlMappingNode rootNode,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        IEnumerable<string> clearPaths,
        ConfigSharingOptions options
    )
    {
        var rulesByConfigPath = layout.GetRulesByConfigPath();
        YamlNode currentNode = rootNode; // Start at the actual root

        // Handle RootKey (like stability_matrix) if specified
        if (!string.IsNullOrEmpty(options.RootKey))
        {
            var rootKeyNode = new YamlScalarNode(options.RootKey);
            if (
                !rootNode.Children.TryGetValue(rootKeyNode, out var subNode)
                || subNode is not YamlMappingNode subMapping
            )
            {
                if (subNode != null)
                    rootNode.Children.Remove(rootKeyNode); // Remove if exists but wrong type
                subMapping = [];
                rootNode.Add(rootKeyNode, subMapping);
            }
            currentNode = subMapping; // Operate within the specified RootKey node
        }

        if (currentNode is not YamlMappingNode writableNode)
        {
            // This should not happen if RootKey logic is correct, but handle defensively
            System.Diagnostics.Debug.WriteLine($"Error: Target node for YAML updates is not a mapping node.");
            return;
        }

        foreach (var (configPath, rule) in rulesByConfigPath)
        {
            var paths = pathsSelector(rule).ToArray();
            var normalizedPaths = paths.Select(p => p.Replace('\\', '/')).ToArray();

            YamlNode? valueNode = null;
            if (normalizedPaths.Length > 0)
            {
                // Use Sequence for multiple paths
                /*valueNode = new YamlSequenceNode(
                    normalizedPaths.Select(p => new YamlScalarNode(p)).Cast<YamlNode>()
                );*/
                // --- Multi-line literal scalar (ComfyUI default) ---
                var multiLinePath = string.Join("\n", normalizedPaths);
                valueNode = new YamlScalarNode(multiLinePath) { Style = ScalarStyle.Literal };
            }

            SetYamlValue(writableNode, configPath, valueNode); // Use helper
        }

        // Clear specified paths
        foreach (var clearPath in clearPaths)
        {
            SetYamlValue(rootNode, clearPath, null); // Note we use root node here instead
        }

        // Optional: Cleanup empty nodes after setting values (could be complex)
    }

    /*private static void UpdateYamlConfig(
        SharedFolderLayout layout,
        YamlMappingNode rootNode,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        ConfigSharingOptions options
    )
    {
        var rulesByConfigPath = layout.GetRulesByConfigPath();
        var smKeyNode = new YamlScalarNode(options.RootKey);

        // Find or create the root key node (e.g., "stability_matrix:")
        if (
            !rootNode.Children.TryGetValue(smKeyNode, out var smPathsNode)
            || smPathsNode is not YamlMappingNode smPathsMapping
        )
        {
            // If it exists but isn't a mapping, remove it first (or handle error)
            if (smPathsNode != null)
            {
                rootNode.Children.Remove(smKeyNode);
            }
            smPathsMapping = new YamlMappingNode();
            rootNode.Add(smKeyNode, smPathsMapping);
        }

        // Get all keys defined in the layout rules to manage removal later
        var allRuleKeys = rulesByConfigPath.Keys.Select(k => new YamlScalarNode(k)).ToHashSet();
        var currentKeysInSmNode = smPathsMapping.Children.Keys.ToHashSet();

        foreach (var (configPath, rule) in rulesByConfigPath)
        {
            var paths = pathsSelector(rule).ToArray();
            var keyNode = new YamlScalarNode(configPath);

            if (paths.Length > 0)
            {
                // Represent multiple paths as a YAML sequence (list) for clarity
                // Normalize paths - YAML generally prefers forward slashes
                var normalizedPaths = paths
                    .Select(p => new YamlScalarNode(p.Replace('\\', '/')))
                    .Cast<YamlNode>()
                    .ToList();
                smPathsMapping.Children[keyNode] = new YamlSequenceNode(normalizedPaths);

                // --- Alternatively, represent as multi-line literal scalar (like ComfyUI default) ---
                // var multiLinePath = string.Join("\n", paths.Select(p => p.Replace('\\', '/')));
                // var valueNode = new YamlScalarNode(multiLinePath) { Style = ScalarStyle.Literal };
                // smPathsMapping.Children[keyNode] = valueNode;
                // ---------------------------------------------------------------------------------
            }
            else
            {
                // No paths for this rule, remove the key from the SM node
                smPathsMapping.Children.Remove(keyNode);
            }
        }

        // Remove any keys under the SM node that are no longer defined by any rule
        foreach (var existingKey in currentKeysInSmNode)
        {
            if (!allRuleKeys.Any(ruleKey => ruleKey.Value == existingKey.ToString()))
            {
                smPathsMapping.Children.Remove(existingKey);
            }
        }

        // If the SM node becomes empty, remove it entirely
        if (smPathsMapping.Children.Count == 0)
        {
            rootNode.Children.Remove(smKeyNode);
        }
    }*/

    private static void SetYamlValue(YamlMappingNode rootMapping, string dottedPath, YamlNode? value)
    {
        var segments = dottedPath.Split('.');
        var currentMapping = rootMapping;

        // Traverse or create nodes up to the parent of the target
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segmentNode = new YamlScalarNode(segments[i]);
            if (
                !currentMapping.Children.TryGetValue(segmentNode, out var nextNode)
                || nextNode is not YamlMappingNode nextMapping
            )
            {
                // If node doesn't exist or isn't a mapping, create it
                if (nextNode != null)
                    currentMapping.Children.Remove(segmentNode); // Remove if wrong type
                nextMapping = [];
                currentMapping.Add(segmentNode, nextMapping);
            }
            currentMapping = nextMapping;
        }

        var finalSegmentNode = new YamlScalarNode(segments[^1]);

        if (value != null)
        {
            // Set or replace the value
            currentMapping.Children[finalSegmentNode] = value;
        }
        else
        {
            // Remove the key if value is null
            currentMapping.Children.Remove(finalSegmentNode);
            // Optional: Cleanup empty parent nodes recursively (more complex)
        }
    }
}
