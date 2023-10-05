namespace StabilityMatrix.Core.Models.Packages;

public class PackageVersionOptions
{
    public IEnumerable<PackageVersion> AvailableVersions { get; set; } =
        Enumerable.Empty<PackageVersion>();
    public IEnumerable<PackageVersion> AvailableBranches { get; set; } =
        Enumerable.Empty<PackageVersion>();
}
