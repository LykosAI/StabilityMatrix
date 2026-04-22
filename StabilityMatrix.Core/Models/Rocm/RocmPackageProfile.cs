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
    /// Requirement files to install after helper-owned ROCm runtime / torch bootstrap steps complete.
    /// </summary>
    public IEnumerable<string> RequirementsFilePaths { get; init; } = ["requirements.txt"];

    /// <summary>
    /// Package requirement entries to exclude because the helper installs them from ROCm-specific feeds.
    /// </summary>
    public string RequirementsExcludePattern { get; init; } = @"(torch(vision|audio)?|xformers)([^a-z].*)?";

    /// <summary>
    /// Extra package-specific pip arguments to include when installing requirements after helper bootstrap.
    /// </summary>
    public IEnumerable<string> ExtraInstallPipArgs { get; init; } = [];

    /// <summary>
    /// Extra package-specific pip arguments to install after requirements and torch are complete.
    /// </summary>
    public IEnumerable<string> PostInstallPipArgs { get; init; } = [];

    /// <summary>
    /// When true, helper-managed requirements installs should use --upgrade.
    /// </summary>
    public bool UpgradePackages { get; init; }

    /// <summary>
    /// When true, helper-managed torch installs should force reinstall the selected ROCm wheel set.
    /// </summary>
    public bool ForceReinstallTorch { get; init; } = true;

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
