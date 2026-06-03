using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Captures the canonical Windows ROCm machine state resolved by the helper.
/// </summary>
public class RocmMachineState
{
    public bool IsCompatible { get; init; }

    public string? FailureReason { get; init; }

    public GpuInfo? SelectedGpu { get; init; }

    public string? RuntimeGfxArch { get; init; }

    public string? MultiArchDeviceExtra { get; init; }
}
