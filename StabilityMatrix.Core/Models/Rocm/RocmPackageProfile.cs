using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Declares what a package expects from the ROCm helper.
/// Package classes should describe intent here rather than hardcoding ROCm decisions inline.
/// </summary>
public class RocmPackageProfile
{
    public bool RequiresRocmSdk { get; init; }

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
