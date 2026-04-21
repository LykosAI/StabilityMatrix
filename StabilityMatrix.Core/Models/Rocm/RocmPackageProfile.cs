using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Declares what a package expects from the ROCm helper.
/// Package classes should describe intent here rather than hardcoding ROCm decisions inline.
/// </summary>
public class RocmPackageProfile
{
    /// <summary>
    /// Logical package name for diagnostics and profile-specific decisions.
    /// </summary>
    public string PackageName { get; init; } = string.Empty;

    public bool RequiresWindows { get; init; }

    public bool RequiresRocmSdk { get; init; }

    public bool NeedsRuntimeGfxResolution { get; init; }

    public bool NeedsHipPath { get; init; }

    public bool NeedsRocmPath { get; init; }

    public bool NeedsTritonOverrideArch { get; init; }

    public bool NeedsRdna1Override { get; init; }

    public bool NeedsLegacySdpFallback { get; init; }

    public bool NeedsAotritonExperimental { get; init; }

    public bool NeedsTunableOpCache { get; init; }

    public bool NeedsTritonCache { get; init; }

    public bool NeedsMIOpenDbPaths { get; init; }

    public bool NeedsRocblasPaths { get; init; }

    /// <summary>
    /// Optional callback for package-specific cache path variables.
    /// The helper will eventually merge these with its own defaults.
    /// </summary>
    public Func<string, IReadOnlyDictionary<string, string>>? CacheDirectoryFactory { get; init; }

    /// <summary>
    /// Optional callback for package-specific environment variables derived from a resolved ROCm context.
    /// </summary>
    public Func<
        RocmRuntimeContext,
        IReadOnlyDictionary<string, string>
    >? ExtraEnvironmentFactory { get; init; }

    /// <summary>
    /// Optional progress message prefix or label that package code can surface during install/update work.
    /// </summary>
    public string? ProgressLabel { get; init; }

    /// <summary>
    /// Controls how helper, package, and user-defined environment variables should be merged.
    /// </summary>
    public RocmEnvironmentOptions EnvironmentOptions { get; init; } = new();
}
