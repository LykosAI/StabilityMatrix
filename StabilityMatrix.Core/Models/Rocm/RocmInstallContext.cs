using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Captures ROCm-related facts needed during package install or update flows.
/// </summary>
public class RocmInstallContext
{
    public string? PreferredGfxArch { get; init; }

    public string? RuntimeGfxArch { get; init; }

    public string? RocmPackageIndexUrl { get; init; }

    public string? RocmTorchIndexUrl { get; init; }

    public TorchIndex TorchIndex { get; init; } = TorchIndex.Rocm;

    public string? WheelCompatibilityHints { get; init; }

    public string? SdkRoot { get; init; }

    public RocmSdkPaths SdkPaths { get; init; } = new();

    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}
