namespace StabilityMatrix.Core.Models.Packages;

public class PackageVersionOptions
{
    public IEnumerable<PackageVersion>? AvailableVersions { get; set; }
    public IEnumerable<PackageVersion>? AvailableBranches { get; set; }
}
