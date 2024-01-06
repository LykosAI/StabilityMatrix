using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record InstalledPackageExtension
{
    /// <summary>
    /// All folders or files of the extension.
    /// </summary>
    public required IEnumerable<IPathObject> Paths { get; init; }

    /// <summary>
    /// The version of the extension.
    /// </summary>
    public PackageExtensionVersion? Version { get; init; }

    /// <summary>
    ///
    /// </summary>
    public PackageExtension? Definition { get; init; }
}
