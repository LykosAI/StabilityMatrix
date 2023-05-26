using System;
using System.Collections.Generic;

namespace StabilityMatrix.Models;

public class Settings
{
    public string Theme { get; set; }
    public List<InstalledPackage> InstalledPackages { get; set; } = new();
    public Guid? ActiveInstalledPackage { get; set; }
    public bool HasInstalledPip { get; set; }
    public bool HasInstalledVenv { get; set; }
}
