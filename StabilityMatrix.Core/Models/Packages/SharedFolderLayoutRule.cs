namespace StabilityMatrix.Core.Models.Packages;

public readonly record struct SharedFolderLayoutRule
{
    public SharedFolderType[] SourceTypes { get; init; }

    public string[] TargetRelativePaths { get; init; }

    public string[] ConfigDocumentPaths { get; init; }

    public SharedFolderLayoutRule()
    {
        SourceTypes = [];
        TargetRelativePaths = [];
        ConfigDocumentPaths = [];
    }

    public SharedFolderLayoutRule(SharedFolderType[] types, string[] targets)
    {
        SourceTypes = types;
        TargetRelativePaths = targets;
    }

    public SharedFolderLayoutRule(SharedFolderType[] types, string[] targets, string[] configs)
    {
        SourceTypes = types;
        TargetRelativePaths = targets;
        ConfigDocumentPaths = configs;
    }

    public SharedFolderLayoutRule Union(SharedFolderLayoutRule other)
    {
        return this with
        {
            SourceTypes = SourceTypes.Union(other.SourceTypes).ToArray(),
            TargetRelativePaths = TargetRelativePaths.Union(other.TargetRelativePaths).ToArray(),
            ConfigDocumentPaths = ConfigDocumentPaths.Union(other.ConfigDocumentPaths).ToArray()
        };
    }
}
