using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record InstalledPackageExtension
{
    /// <summary>
    /// All folders or files of the extension.
    /// </summary>
    public required IEnumerable<IPathObject> Paths { get; init; }

    /// <summary>
    /// Primary path of the extension.
    /// </summary>
    public IPathObject? PrimaryPath => Paths.FirstOrDefault();

    /// <summary>
    /// The version of the extension.
    /// </summary>
    public PackageExtensionVersion? Version { get; init; }

    /// <summary>
    /// Remote git repository url, if the extension is a git repository.
    /// </summary>
    public string? GitRepositoryUrl { get; init; }

    /// <summary>
    /// The PackageExtension definition, if available.
    /// </summary>
    public PackageExtension? Definition { get; init; }

    public string Title
    {
        get
        {
            if (Definition?.Title is { } title)
            {
                return title;
            }

            if (Paths.FirstOrDefault()?.Name is { } pathName)
            {
                return pathName;
            }

            return "";
        }
    }

    /// <summary>
    /// Path containing PrimaryPath and its parent.
    /// </summary>
    public string DisplayPath =>
        PrimaryPath switch
        {
            null => "",
            DirectoryPath { Parent: { } parentDir } dir => $"{parentDir.Name}/{dir.Name}",
            _ => PrimaryPath.Name
        };
}
