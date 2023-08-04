using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models;


/// <summary>
/// Pair of InstalledPackage and BasePackage
/// </summary>
public record PackagePair(InstalledPackage InstalledPackage, BasePackage BasePackage);
