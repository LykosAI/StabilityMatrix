using System.Text.Json;

namespace StabilityMatrix.Core.Models.Packages.Config;

// Options might need expansion later if format-specific settings are required
public record ConfigSharingOptions
{
    public static ConfigSharingOptions Default { get; } = new();

    // For JSON:
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new() { WriteIndented = true };

    // For JSON/YAML: Write single paths as arrays?
    public bool AlwaysWriteArray { get; set; } = false;

    // For YAML/FDS: Key under which to store SM paths (e.g., "stability_matrix")
    public string? RootKey { get; set; }

    // Do we want to clear the root key / set to relative paths when clearing?
    public ConfigDefaultType ConfigDefaultType { get; set; } = ConfigDefaultType.TargetRelativePaths;
}
