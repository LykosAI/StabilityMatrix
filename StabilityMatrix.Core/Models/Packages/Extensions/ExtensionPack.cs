namespace StabilityMatrix.Core.Models.Packages.Extensions;

public class ExtensionPack
{
    public required string Name { get; set; }
    public required string PackageType { get; set; }
    public List<SavedPackageExtension> Extensions { get; set; } = [];
}
