namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Controls how ROCm helper defaults, package-specific variables, and user overrides are layered at launch.
/// </summary>
public class RocmEnvironmentOptions
{
    /// <summary>
    /// When true, package-specific environment additions may be merged on top of helper defaults.
    /// </summary>
    public bool IncludePackageOverrides { get; init; } = true;

    /// <summary>
    /// When true, user-defined Stability Matrix environment variables may override helper/package defaults last.
    /// </summary>
    public bool IncludeUserOverrides { get; init; } = true;

    /// <summary>
    /// When set, overrides the default PyTorch allocator tuning string added by the ROCm helper.
    /// </summary>
    public string? PyTorchAllocConf { get; init; } = "max_split_size_mb:512,garbage_collection_threshold:0.8";

    /// <summary>
    /// When set, configures MIOpen find mode for helper-managed ROCm defaults.
    /// </summary>
    public string? MiopenFindMode { get; init; } = "2";

    /// <summary>
    /// When set, configures MIOpen search cutoff for helper-managed ROCm defaults.
    /// </summary>
    public string? MiopenSearchCutoff { get; init; } = "1";

    /// <summary>
    /// When set, configures MIOpen find enforcement behavior for helper-managed ROCm defaults.
    /// </summary>
    public string? MiopenFindEnforce { get; init; } = "3";

    /// <summary>
    /// When set, controls whether AMD Triton-backed flash attention is enabled by helper defaults.
    /// </summary>
    public string? FlashAttentionTritonAmdEnable { get; init; } = "TRUE";

    /// <summary>
    /// When true, helper-managed defaults will enable ROCm AOTriton on modern Windows ROCm architectures.
    /// </summary>
    public bool ApplyAotritonExperimental { get; init; } = true;

    /// <summary>
    /// When true, helper-managed defaults will force math SDP on legacy ROCm architectures.
    /// </summary>
    public bool ApplyLegacySdpFallback { get; init; } = true;

    /// <summary>
    /// When true, helper-managed defaults will apply the RDNA1 HSA override mask when needed.
    /// </summary>
    public bool ApplyRdna1Override { get; init; } = true;
}
