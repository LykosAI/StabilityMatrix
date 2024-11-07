namespace StabilityMatrix.Core.Models.Packages.Extensions;

public class SavedPackageExtension
{
    public required PackageExtension PackageExtension { get; set; }
    public PackageExtensionVersion? Version { get; set; }
    public bool AlwaysUseLatest { get; set; }
}
