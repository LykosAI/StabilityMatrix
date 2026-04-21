namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Controls how helper-generated, package-specific, and user-defined environment variables
/// should be layered together once the helper has real behavior.
/// </summary>
public class RocmEnvironmentOptions
{
    /// <summary>
    /// Determines the merge order used when multiple environment sources provide the same key.
    /// </summary>
    public RocmEnvironmentOverlayPriority OverlayPriority { get; init; } =
        RocmEnvironmentOverlayPriority.HelperThenPackageThenUser;

    /// <summary>
    /// When true, package-specific environment additions may be merged on top of helper defaults.
    /// </summary>
    public bool IncludePackageOverrides { get; init; } = true;

    /// <summary>
    /// When true, user-defined Stability Matrix environment variables may be merged last.
    /// </summary>
    public bool IncludeUserOverrides { get; init; } = true;
}

/// <summary>
/// Describes the intended precedence of environment sources for ROCm-enabled package launches.
/// </summary>
public enum RocmEnvironmentOverlayPriority
{
    HelperThenPackageThenUser,
    HelperThenUserThenPackage,
}
