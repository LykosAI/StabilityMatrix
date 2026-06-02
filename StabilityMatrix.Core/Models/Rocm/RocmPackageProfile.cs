using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Declares what a package expects from the ROCm helper.
/// Package classes should describe intent here rather than hardcoding ROCm decisions inline.
/// </summary>
public class RocmPackageProfile
{
    /// <summary>
    /// Standard package install configuration used before the helper-managed Windows ROCm torch step.
    /// </summary>
    public PipInstallConfig InstallConfig { get; init; } = new();

    /// <summary>
    /// Optional callback for package-specific environment variables derived from a resolved ROCm context.
    /// </summary>
    public Func<
        RocmRuntimeContext,
        IReadOnlyDictionary<string, string>
    >? ExtraEnvironmentFactory { get; init; }

    /// <summary>
    /// Controls whether package-specific environment variables should be layered on top of helper defaults.
    /// </summary>
    public RocmEnvironmentOptions EnvironmentOptions { get; init; } = new();

    /// <summary>
    /// Allows a package profile to adjust default launch options for Windows ROCm.
    /// Default implementation is a no-op; profiles that need package-specific adjustments
    /// should override this method.
    /// </summary>
    public virtual void ApplyWindowsRocmLaunchDefaults(
        List<LaunchOptionDefinition> launchOptions,
        IRocmPackageHelper rocmPackageHelper
    ) { }

    /// <summary>
    /// Returns a package-specific preferred cross-attention argument for Windows ROCm launches.
    /// Default implementation returns <c>null</c> indicating no preference.
    /// </summary>
    public virtual string? GetPreferredCrossAttentionArgument(IRocmPackageHelper rocmPackageHelper)
    {
        return null;
    }
}
