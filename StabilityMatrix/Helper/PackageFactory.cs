using System;
using System.Collections.Generic;
using System.Linq;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public class PackageFactory : IPackageFactory
{
    private readonly IEnumerable<BasePackage> basePackages;

    public PackageFactory(IEnumerable<BasePackage> basePackages)
    {
        this.basePackages = basePackages;
    }
    
    public IEnumerable<BasePackage> GetAllAvailablePackages()
    {
        return basePackages;
    }

    public BasePackage? FindPackageByName(string packageName)
    {
        return basePackages.FirstOrDefault(x => x.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
    }
}
