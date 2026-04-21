using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Captures resolved ROCm facts for a package launch or runtime decision.
/// This model is intended to separate hardware/runtime facts from package policy.
/// </summary>
public class RocmRuntimeContext
{
    public bool IsSupported { get; init; }

    public string? FailureReason { get; init; }

    public GpuInfo? SelectedGpu { get; init; }

    public string? RuntimeGfxArch { get; init; }

    public bool IsLegacyGpu { get; init; }

    public bool IsRdna1 { get; init; }

    public string? HipPath { get; init; }

    public string? RocmPath { get; init; }

    public string? RocmSdkSitePackagesPath { get; init; }

    public RocmSdkPaths SdkPaths { get; init; } = new();

    public IReadOnlyDictionary<string, string> ResolvedEnvironment { get; init; } =
        new Dictionary<string, string>();
}
