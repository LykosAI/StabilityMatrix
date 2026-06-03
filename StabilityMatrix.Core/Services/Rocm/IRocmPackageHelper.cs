using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Services.Rocm;

/// <summary>
/// Defines the ROCm helper surface area shared by ROCm-capable packages.
/// </summary>
public interface IRocmPackageHelper
{
    /// <summary>
    /// Evaluates whether the current machine is compatible with ROCm.
    /// </summary>
    RocmCompatibilityResult GetCompatibility();

    /// <summary>
    /// Builds a launch-time environment dictionary from resolved ROCm runtime data.
    /// </summary>
    IReadOnlyDictionary<string, string> BuildLaunchEnvironment(RocmPackageProfile profile);

    /// <summary>
    /// Returns shared Windows ROCm launch notice lines if the current machine and selected torch index
    /// qualify for the Windows native ROCm launch environment; otherwise returns an empty list.
    /// </summary>
    IReadOnlyList<string> GetWindowsLaunchNoticeLines(TorchIndex selectedTorchIndex);

    /// <summary>
    /// Returns true when the current machine is Windows, the selected torch index is ROCm,
    /// and the machine is compatible with Windows native ROCm.
    /// </summary>
    bool ShouldApplyWindowsLaunchEnvironment(TorchIndex selectedTorchIndex);

    /// <summary>
    /// Ensures a usable Windows ROCm SDK devel package is installed from the ROCm multi-arch index,
    /// preferring the same build date token as the installed torch build and falling back to the latest available build.
    /// </summary>
    Task EnsureWindowsSdkDevelAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Builds the standard pip install config for a Windows ROCm package, ensuring torch installation stays helper-managed.
    /// </summary>
    PipInstallConfig BuildWindowsNativeInstallConfig(RocmPackageProfile profile);

    /// <summary>
    /// Installs the Windows-native ROCm torch wheel set for a package using helper-resolved multi-arch device extras.
    /// </summary>
    Task InstallWindowsNativeTorchAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );
}
