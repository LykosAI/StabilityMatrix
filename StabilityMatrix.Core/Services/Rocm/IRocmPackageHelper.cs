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
    /// Returns shared ROCm launch notice lines when the selected torch index qualifies for the
    /// helper-managed ROCm launch environment on this machine; otherwise returns an empty list.
    /// </summary>
    IReadOnlyList<string> GetLaunchNoticeLines(TorchIndex selectedTorchIndex);

    /// <summary>
    /// Returns true when the selected torch index is ROCm and the helper can manage a native ROCm
    /// install on the current machine.
    /// </summary>
    bool ShouldApplyLaunchEnvironment(TorchIndex selectedTorchIndex);

    /// <summary>
    /// Ensures a usable ROCm SDK devel package is installed from the ROCm index,
    /// preferring the same build date token as the installed torch build and falling back to the latest available build.
    /// </summary>
    Task EnsureRocmSdkDevelAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Builds the standard pip install config for a ROCm package, ensuring torch installation stays
    /// helper-managed.
    /// </summary>
    PipInstallConfig BuildRocmInstallConfig(RocmPackageProfile profile);

    /// <summary>
    /// Installs the ROCm torch wheel set for a package from the resolved index, selecting device code
    /// through the index's device extra when it has one.
    /// </summary>
    Task InstallRocmTorchAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );
}
