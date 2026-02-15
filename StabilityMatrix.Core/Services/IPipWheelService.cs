using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Service for installing pip wheel packages from GitHub releases.
/// All install methods are safe to call regardless of platform/GPU support -
/// they will silently no-op if the package is not applicable.
/// </summary>
public interface IPipWheelService
{
    /// <summary>
    /// Installs Triton. Windows uses triton-windows, Linux uses triton.
    /// No-ops on macOS.
    /// </summary>
    Task InstallTritonAsync(
        IPyVenvRunner venv,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    );

    /// <summary>
    /// Installs SageAttention from pre-built wheels or source.
    /// No-ops on macOS or non-NVIDIA GPUs.
    /// </summary>
    Task InstallSageAttentionAsync(
        IPyVenvRunner venv,
        GpuInfo? gpuInfo = null,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    );

    /// <summary>
    /// Installs Nunchaku from pre-built wheels.
    /// No-ops on macOS or GPUs with compute capability &lt; 7.5.
    /// </summary>
    Task InstallNunchakuAsync(
        IPyVenvRunner venv,
        GpuInfo? gpuInfo = null,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    );

    /// <summary>
    /// Installs FlashAttention from pre-built wheels.
    /// Windows only. No-ops on Linux/macOS.
    /// </summary>
    Task InstallFlashAttentionAsync(
        IPyVenvRunner venv,
        IProgress<ProgressReport>? progress = null,
        string? version = null
    );
}
