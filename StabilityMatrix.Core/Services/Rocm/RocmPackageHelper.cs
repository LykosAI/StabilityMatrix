using System.Collections.Immutable;
using Injectio.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Services.Rocm;

/// <summary>
/// Provides the shared ROCm helper surface area used by ROCm-capable packages.
/// </summary>
[RegisterSingleton<IRocmPackageHelper, RocmPackageHelper>]
public class RocmPackageHelper : IRocmPackageHelper
{
    private const string NotImplementedMessage = "ROCm helper behavior has not been implemented yet.";

    /// <inheritdoc />
    public Task<RocmCompatibilityResult> GetCompatibilityAsync(
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(
            new RocmCompatibilityResult { IsCompatible = false, FailureReason = NotImplementedMessage }
        );
    }

    /// <inheritdoc />
    public Task<RocmRuntimeContext> ResolveRuntimeContextAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(
            new RocmRuntimeContext { IsSupported = false, FailureReason = NotImplementedMessage }
        );
    }

    /// <inheritdoc />
    public Task<RocmInstallContext> ResolveInstallContextAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(new RocmInstallContext());
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> BuildInstallEnvironment(
        string installLocation,
        RocmInstallContext context,
        RocmPackageProfile profile
    )
    {
        return new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public Task<RocmInstallContext> RefreshPackageAfterUpdateAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(new RocmInstallContext());
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> BuildLaunchEnvironmentAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    /// <inheritdoc />
    public async Task ApplyLaunchEnvironmentAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        var environment = await BuildLaunchEnvironmentAsync(
                installLocation,
                installedPackage,
                profile,
                cancellationToken
            )
            .ConfigureAwait(false);

        venvRunner.UpdateEnvironmentVariables(env => env.SetItems(environment));
    }
}
