using System;
using System.Collections.Generic;
using System.Linq;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;

namespace StabilityMatrix.Helper;

public static class PackageFactory
{
    private static readonly List<BasePackage> AllPackages = new()
    {
        new A3WebUI(),
        new DankDiffusion()
    };

    public static IEnumerable<BasePackage> GetAllAvailablePackages()
    {
        return AllPackages;
    }

    public static BasePackage? FindPackageByName(string packageName)
    {
        return AllPackages.FirstOrDefault(x => x.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
    }
}
