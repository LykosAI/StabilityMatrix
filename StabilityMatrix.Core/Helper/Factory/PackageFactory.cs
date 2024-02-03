using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Helper.Factory;

[Singleton(typeof(IPackageFactory))]
public class PackageFactory : IPackageFactory
{
    /// <summary>
    /// Mapping of package.Name to package
    /// </summary>
    private readonly Dictionary<string, BasePackage> basePackages;

    public PackageFactory(IEnumerable<BasePackage> basePackages)
    {
        this.basePackages = basePackages.ToDictionary(x => x.Name);
    }

    public IEnumerable<BasePackage> GetAllAvailablePackages()
    {
        return basePackages.Values.OrderBy(p => p.InstallerSortOrder).ThenBy(p => p.DisplayName);
    }

    public BasePackage? FindPackageByName(string? packageName)
    {
        return packageName == null ? null : basePackages.GetValueOrDefault(packageName);
    }

    public BasePackage? this[string packageName] => basePackages[packageName];

    /// <inheritdoc />
    public PackagePair? GetPackagePair(InstalledPackage? installedPackage)
    {
        if (installedPackage?.PackageName is not { } packageName)
            return null;

        return !basePackages.TryGetValue(packageName, out var basePackage)
            ? null
            : new PackagePair(installedPackage, basePackage);
    }

    public IEnumerable<BasePackage> GetPackagesByType(PackageType packageType) =>
        basePackages.Values.Where(p => p.PackageType == packageType);
}
