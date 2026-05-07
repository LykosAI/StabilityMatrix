namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// High-level helper-managed compatibility modes for ROCm torch installation.
/// Package profiles should declare intent here and let the ROCm helper resolve any
/// architecture-specific index or dependency fallback details.
/// </summary>
public enum RocmTorchCompatibilityMode
{
    None,

    /// <summary>
    /// Lets the helper apply built-in Windows ROCm dependency fallback rules when
    /// specific TheRock indexes are missing compatible transitive dependency wheels.
    /// </summary>
    HelperManagedDependencyFallback,
}
