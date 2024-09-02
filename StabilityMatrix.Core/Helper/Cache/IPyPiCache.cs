using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Helper.Cache;

public interface IPyPiCache
{
    Task<IEnumerable<CustomVersion>> GetPackageVersions(string packageName);
}
