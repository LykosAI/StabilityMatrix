namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Captures ROCm-related facts needed during package install or update flows.
/// </summary>
public class RocmInstallContext
{
    public string? RuntimeGfxArch { get; init; }

    public string? RocmPackageIndexUrl { get; init; }
}
