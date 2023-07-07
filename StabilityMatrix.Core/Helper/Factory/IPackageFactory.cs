using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Helper.Factory;

public interface IPackageFactory
{
    IEnumerable<BasePackage> GetAllAvailablePackages();
    BasePackage? FindPackageByName(string packageName);
}
