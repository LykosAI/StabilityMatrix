using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Describes whether a package/profile is currently compatible with ROCm on the active machine.
/// </summary>
public class RocmCompatibilityResult
{
    public bool IsCompatible { get; init; }

    public string? FailureReason { get; init; }

    public GpuInfo? SelectedGpu { get; init; }

    public string? ResolvedGfxArch { get; init; }
}
