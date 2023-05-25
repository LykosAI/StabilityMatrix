using System;
using System.Collections.Generic;

namespace StabilityMatrix.Models;

public class Settings
{
    public string Theme { get; set; }
    public List<InstalledPackage> InstalledPackages { get; set; }
    public Guid? ActiveInstalledPackage { get; set; }
}
