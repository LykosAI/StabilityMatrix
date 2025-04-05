namespace StabilityMatrix.Core.Models.Packages;

public readonly record struct SharedFolderLayoutRule()
{
    public SharedFolderType[] SourceTypes { get; init; } = [];

    public string[] TargetRelativePaths { get; init; } = [];

    public string[] ConfigDocumentPaths { get; init; } = [];

    /// <summary>
    /// For rules that use the root models folder instead of a specific SharedFolderType
    /// </summary>
    public bool IsRoot { get; init; }

    /// <summary>
    /// Optional sub-path from all source types to the target path.
    /// </summary>
    public string? SourceSubPath { get; init; }

    public SharedFolderLayoutRule Union(SharedFolderLayoutRule other)
    {
        return this with
        {
            SourceTypes = SourceTypes.Union(other.SourceTypes).ToArray(),
            TargetRelativePaths = TargetRelativePaths.Union(other.TargetRelativePaths).ToArray(),
            ConfigDocumentPaths = ConfigDocumentPaths.Union(other.ConfigDocumentPaths).ToArray(),
            IsRoot = IsRoot || other.IsRoot,
            SourceSubPath = SourceSubPath ?? other.SourceSubPath
        };
    }
}
