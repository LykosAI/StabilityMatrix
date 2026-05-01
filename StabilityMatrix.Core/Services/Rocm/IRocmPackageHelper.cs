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
    /// Resolves the runtime ROCm facts needed for package launch and environment construction.
    /// </summary>
    RocmRuntimeContext ResolveRuntimeContext(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile
    );

    /// <summary>
    /// Resolves the ROCm facts needed during package installation or update operations.
    /// </summary>
    RocmInstallContext ResolveInstallContext(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile
    );

    /// <summary>
    /// Builds a launch-time environment dictionary from resolved ROCm runtime data.
    /// </summary>
    IReadOnlyDictionary<string, string> BuildLaunchEnvironment(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile
    );

    /// <summary>
    /// Performs the Windows-native ROCm bootstrap/install flow for a package using helper-resolved gfx-family feed URLs.
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
