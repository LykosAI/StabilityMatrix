using StabilityMatrix.Core.Models;
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
    /// Evaluates whether the current machine and package profile are compatible with ROCm.
    /// </summary>
    RocmCompatibilityResult GetCompatibility(RocmPackageProfile profile);

    /// <summary>
    /// Builds a launch-time environment dictionary from resolved ROCm runtime data.
    /// </summary>
    IReadOnlyDictionary<string, string> BuildLaunchEnvironment(RocmPackageProfile profile);

    /// <summary>
    /// Returns shared Windows ROCm launch notice lines for helper-managed packages.
    /// </summary>
    IReadOnlyList<string> GetWindowsLaunchNoticeLines();

    /// <summary>
    /// Ensures a usable Windows ROCm SDK devel package is installed from the ROCm multi-arch index,
    /// preferring the same nightly build date as the installed torch build and falling back to the latest available build.
    /// </summary>
    Task EnsureWindowsSdkDevelAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Performs the Windows-native ROCm install flow for a package using helper-resolved multi-arch device extras.
    /// </summary>
    Task InstallWindowsNativePackageAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );
}
