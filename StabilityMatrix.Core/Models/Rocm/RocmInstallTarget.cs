namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Package index a helper-managed ROCm install resolves torch from. AMD's feeds carry per-architecture
/// device packages and the ROCm SDK; the upstream PyTorch feeds carry neither.
/// </summary>
public enum RocmIndexKind
{
    /// <summary>AMD's stable multi-arch feed, the default on both platforms.</summary>
    AmdStable,

    /// <summary>AMD's nightly multi-arch feed, used for targets the stable feed does not publish.</summary>
    AmdNightly,

    /// <summary>Upstream PyTorch ROCm 7.2 channel, available to a package as an override.</summary>
    PyTorchRocm72,

    /// <summary>Upstream PyTorch ROCm 7.14 channel, available to a package as an override.</summary>
    PyTorchRocm714,
}

/// <summary>
/// Everything the helper needs to install ROCm torch: which index to use, which device
/// package applies, and which index features are available. Resolved from the architecture and the
/// package profile.
/// </summary>
public sealed record RocmInstallTarget
{
    public required RocmIndexKind IndexKind { get; init; }

    public required string IndexUrl { get; init; }

    /// <summary>Per-architecture device package extra, e.g. <c>device-gfx1201</c>. Null when the index has none.</summary>
    public string? DeviceExtra { get; init; }

    public required bool SupportsDeviceExtras { get; init; }

    public required bool SupportsSdkDevel { get; init; }

    /// <summary>
    /// True when torchvision on this index declares no torch constraint, so the helper has to resolve the
    /// torch/torchvision pair and pin both.
    /// </summary>
    public required bool RequiresPairResolution { get; init; }
}
