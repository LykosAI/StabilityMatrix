using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Captures the canonical ROCm machine state resolved by the helper.
/// </summary>
public class RocmMachineState
{
    public bool IsCompatible { get; init; }

    public string? FailureReason { get; init; }

    public GpuInfo? SelectedGpu { get; init; }

    public string? RuntimeGfxArch { get; init; }

    /// <summary>Number of AMD GPUs the machine exposes, GPU agents only.</summary>
    public int AmdDeviceCount { get; init; }

    /// <summary>ROCm device index of the selected GPU, when known.</summary>
    public int? DeviceIndex { get; init; }
}
