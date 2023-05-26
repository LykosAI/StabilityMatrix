using System;

namespace StabilityMatrix.Models;

/// <summary>
/// Profile information for a user-installed package.
/// </summary>
public class InstalledPackage
{
    // Unique ID for the installation
    public Guid Id { get; set; }
    // User defined name
    public string? Name { get; set; }
    // Package name
    public string? PackageName { get; set; }
    // Package version
    public string? PackageVersion { get; set; }
    // Install path
    public string? Path { get; set; }

    public string? LaunchCommand { get; set; }
    
}
