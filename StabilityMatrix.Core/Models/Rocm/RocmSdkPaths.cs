namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Represents ROCm SDK-related paths resolved for a package install.
/// These values are intentionally plain data so package code can decide which paths matter.
/// </summary>
public class RocmSdkPaths
{
    public string? RocmRoot { get; init; }

    public string? HipPath { get; init; }

    public string? RocmPath { get; init; }

    public string? RocmSdkSitePackagesPath { get; init; }

    public string? MioPenDbPath { get; init; }

    public string? RocblasDbPath { get; init; }

    public string? RocblasLibraryPath { get; init; }
}
