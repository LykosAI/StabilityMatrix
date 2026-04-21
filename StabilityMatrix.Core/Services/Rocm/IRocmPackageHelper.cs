using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Rocm;
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
    Task<RocmCompatibilityResult> GetCompatibilityAsync(
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resolves the runtime ROCm facts needed for package launch and environment construction.
    /// </summary>
    Task<RocmRuntimeContext> ResolveRuntimeContextAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resolves the ROCm facts needed during package installation or update operations.
    /// </summary>
    Task<RocmInstallContext> ResolveInstallContextAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Builds an install-time environment dictionary from a resolved install context.
    /// </summary>
    IReadOnlyDictionary<string, string> BuildInstallEnvironment(
        string installLocation,
        RocmInstallContext context,
        RocmPackageProfile profile
    );

    /// <summary>
    /// Re-resolves ROCm install facts after a package update changes dependencies or runtime state.
    /// </summary>
    Task<RocmInstallContext> RefreshPackageAfterUpdateAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Builds a launch-time environment dictionary from resolved ROCm runtime data.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> BuildLaunchEnvironmentAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Applies a resolved launch environment to the provided Python venv runner.
    /// </summary>
    Task ApplyLaunchEnvironmentAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    );
}
