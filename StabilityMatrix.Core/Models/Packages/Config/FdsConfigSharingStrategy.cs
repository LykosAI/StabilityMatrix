using FreneticUtilities.FreneticDataSyntax;

namespace StabilityMatrix.Core.Models.Packages.Config;

public class FdsConfigSharingStrategy : IConfigSharingStrategy
{
    public async Task UpdateAndWriteAsync(
        Stream configStream,
        SharedFolderLayout layout,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        ConfigSharingOptions options,
        CancellationToken cancellationToken = default
    )
    {
        FDSSection rootSection;
        var initialPosition = configStream.Position;
        var isEmpty = configStream.Length - initialPosition == 0;

        if (!isEmpty)
        {
            try
            {
                // FDSUtility reads from the current position
                using var reader = new StreamReader(configStream, leaveOpen: true);
                var fdsContent = await reader.ReadToEndAsync().ConfigureAwait(false);
                rootSection = new FDSSection(fdsContent);
            }
            catch (Exception ex) // FDSUtility might throw various exceptions on parse errors
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error deserializing FDS config: {ex.Message}. Treating as new."
                );
                rootSection = new FDSSection();
                isEmpty = true;
            }
        }
        else
        {
            rootSection = new FDSSection();
        }

        UpdateFdsConfig(layout, rootSection, pathsSelector, options);

        // Reset stream to original position before writing
        configStream.Seek(initialPosition, SeekOrigin.Begin);
        // Truncate the stream
        configStream.SetLength(initialPosition + 0);

        // Save using a StreamWriter to control encoding and leave stream open
        await using (var writer = new StreamWriter(configStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            await writer
                .WriteAsync(rootSection.SaveToString().AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false); // Ensure content is written
        }
        await configStream.FlushAsync(cancellationToken).ConfigureAwait(false); // Flush the underlying stream
    }

    private static void UpdateFdsConfig(
        SharedFolderLayout layout,
        FDSSection rootSection,
        Func<SharedFolderLayoutRule, IEnumerable<string>> pathsSelector,
        ConfigSharingOptions options
    )
    {
        var rulesByConfigPath = layout.GetRulesByConfigPath();

        // SwarmUI typically stores paths under a "Paths" section
        var pathsSection = rootSection.GetSection("Paths") ?? new FDSSection(); // Get or create "Paths" section

        // Keep track of keys managed by the layout to remove old ones
        var allRuleKeys = rulesByConfigPath.Keys.ToHashSet();
        var currentKeysInPathsSection = pathsSection.GetRootKeys(); // Assuming FDS has a way to list keys

        foreach (var (configPath, rule) in rulesByConfigPath)
        {
            var paths = pathsSelector(rule).ToArray();

            // Normalize paths for FDS - likely prefers native OS slashes or forward slashes
            var normalizedPaths = paths.Select(p => p.Replace('/', Path.DirectorySeparatorChar)).ToList();

            if (normalizedPaths.Count > 0)
            {
                // FDS might store lists separated by newline or another char, or just the first path?
                // Assuming SwarmUI expects a single path string per key, potentially the first one.
                // If it supports lists (e.g., newline separated), adjust here.
                // For now, let's assume it takes the first path if multiple are generated,
                // or handles lists internally if the key implies it (needs SwarmUI knowledge).
                pathsSection.Set(configPath, normalizedPaths.First());

                // If FDS supports lists explicitly (e.g., via SetList), use that:
                // pathsSection.SetList(configPath, normalizedPaths);
            }
            else
            {
                // No paths for this rule, remove the key
                pathsSection.Remove(configPath); // Assuming Remove method exists
            }
        }

        // Remove any keys in the Paths section that are no longer defined by any rule
        foreach (var existingKey in currentKeysInPathsSection)
        {
            if (!allRuleKeys.Contains(existingKey))
            {
                pathsSection.Remove(existingKey);
            }
        }

        // If the Paths section is not empty, add/update it in the root
        if (pathsSection.GetRootKeys().Any()) // Check if the section has content
        {
            rootSection.Set("Paths", pathsSection);
            // rootSection.SetSection("Paths", pathsSection);
        }
        else // Otherwise, remove the empty Paths section from the root
        {
            rootSection.Remove("Paths"); // Assuming Remove method exists for sections too
        }
    }
}
