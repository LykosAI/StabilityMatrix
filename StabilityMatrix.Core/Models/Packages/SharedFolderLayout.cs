using System.Collections.Immutable;
using StabilityMatrix.Core.Models.Packages.Config;

namespace StabilityMatrix.Core.Models.Packages;

public record SharedFolderLayout
{
    /// <summary>
    /// Optional config file path, relative from package installation directory
    /// </summary>
    public string? RelativeConfigPath { get; set; }

    public ConfigFileType? ConfigFileType { get; set; }

    public ConfigSharingOptions ConfigSharingOptions { get; set; } = ConfigSharingOptions.Default;

    public IImmutableList<SharedFolderLayoutRule> Rules { get; set; } = [];

    public Dictionary<string, SharedFolderLayoutRule> GetRulesByConfigPath()
    {
        // Dictionary of config path to rule
        var configPathToRule = new Dictionary<string, SharedFolderLayoutRule>();

        foreach (var rule in Rules)
        {
            // Ignore rules without config paths
            if (rule.ConfigDocumentPaths is not { Length: > 0 } configPaths)
            {
                continue;
            }

            foreach (var configPath in configPaths)
            {
                // Get or create rule
                var existingRule = configPathToRule.GetValueOrDefault(
                    configPath,
                    new SharedFolderLayoutRule()
                );

                // Add unique
                configPathToRule[configPath] = existingRule.Union(rule);
            }
        }

        return configPathToRule;
    }
}
