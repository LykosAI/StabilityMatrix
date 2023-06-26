using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models;

/// <summary>
/// Profile information for a user-installed package.
/// </summary>
public class InstalledPackage
{
    // Unique ID for the installation
    public Guid Id { get; set; }
    // User defined name
    public string? DisplayName { get; set; }
    // Package name
    public string? PackageName { get; set; }
    // Package version
    public string? PackageVersion { get; set; }
    public string? InstalledBranch { get; set; }
    public string? DisplayVersion { get; set; }
    
    // Old type absolute path
    [Obsolete("Use LibraryPath instead. (Kept for migration)")]
    public string? Path { get; set; }
    
    /// <summary>
    /// Relative path from the library root.
    /// </summary>
    public string? LibraryPath { get; set; }
    
    /// <summary>
    /// Full path to the package, using LibraryPath and GlobalConfig.LibraryDir.
    /// </summary>
    public string? FullPath => LibraryPath != null ? System.IO.Path.Combine(GlobalConfig.LibraryDir, LibraryPath) : null;
    
    public string? LaunchCommand { get; set; }
    public List<LaunchOption>? LaunchArgs { get; set; }
    public DateTimeOffset? LastUpdateCheck { get; set; }

    [JsonIgnore] public bool UpdateAvailable { get; set; }
}
