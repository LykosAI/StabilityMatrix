using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;

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
}
