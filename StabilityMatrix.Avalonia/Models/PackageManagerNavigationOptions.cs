using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.Models;

public record PackageManagerNavigationOptions
{
    public bool OpenInstallerDialog { get; init; }

    public BasePackage? InstallerSelectedPackage { get; init; }
}
