using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace StabilityMatrix.Core.Models.Api.Comfy;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public readonly record struct ComfySampler(string Name)
{
    public static ComfySampler Euler { get; } = new("euler");
    public static ComfySampler EulerAncestral { get; } = new("euler_ancestral");
    public static ComfySampler Heun { get; } = new("heun");
    public static ComfySampler Dpm2 { get; } = new("dpm_2");
    public static ComfySampler Dpm2Ancestral { get; } = new("dpm_2_ancestral");
    public static ComfySampler LMS { get; } = new("lms");
    public static ComfySampler DpmFast { get; } = new("dpm_fast");
    public static ComfySampler DpmAdaptive { get; } = new("dpm_adaptive");
    public static ComfySampler Dpmpp2SAncestral { get; } = new("dpmpp_2s_ancestral");
    public static ComfySampler DpmppSde { get; } = new("dpmpp_sde");
    public static ComfySampler DpmppSdeGpu { get; } = new("dpmpp_sde_gpu");
    public static ComfySampler Dpmpp2M { get; } = new("dpmpp_2m");
    public static ComfySampler Dpmpp2MSde { get; } = new("dpmpp_2m_sde");
    public static ComfySampler Dpmpp2MSdeGpu { get; } = new("dpmpp_2m_sde_gpu");
    public static ComfySampler Dpmpp3M { get; } = new("dpmpp_3m");
    public static ComfySampler Dpmpp3MSde { get; } = new("dpmpp_3m_sde");
    public static ComfySampler Dpmpp3MSdeGpu { get; } = new("dpmpp_3m_sde_gpu");
    public static ComfySampler DDIM { get; } = new("ddim");
    public static ComfySampler UniPC { get; } = new("uni_pc");
    public static ComfySampler UniPCBh2 { get; } = new("uni_pc_bh2");

    private static Dictionary<ComfySampler, string> ConvertDict { get; } =
        new()
        {
            [Euler] = "Euler",
            [EulerAncestral] = "Euler Ancestral",
            [Heun] = "Heun",
            [Dpm2] = "DPM 2",
            [Dpm2Ancestral] = "DPM 2 Ancestral",
            [LMS] = "LMS",
            [DpmFast] = "DPM Fast",
            [DpmAdaptive] = "DPM Adaptive",
            [Dpmpp2SAncestral] = "DPM++ 2S Ancestral",
            [DpmppSde] = "DPM++ SDE",
            [DpmppSdeGpu] = "DPM++ SDE GPU",
            [Dpmpp2M] = "DPM++ 2M",
            [Dpmpp2MSde] = "DPM++ 2M SDE",
            [Dpmpp2MSdeGpu] = "DPM++ 2M SDE GPU",
            [Dpmpp3M] = "DPM++ 3M",
            [Dpmpp3MSde] = "DPM++ 3M SDE",
            [Dpmpp3MSdeGpu] = "DPM++ 3M SDE GPU",
            [DDIM] = "DDIM",
            [UniPC] = "UniPC",
            [UniPCBh2] = "UniPC BH2"
        };

    public static IReadOnlyList<ComfySampler> Defaults { get; } =
        ConvertDict.Keys.ToImmutableArray();

    public string DisplayName =>
        ConvertDict.TryGetValue(this, out var displayName) ? displayName : Name;

    /// <inheritdoc />
    public bool Equals(ComfySampler other)
    {
        return Name == other.Name;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    private sealed class NameEqualityComparer : IEqualityComparer<ComfySampler>
    {
        public bool Equals(ComfySampler x, ComfySampler y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(ComfySampler obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    public static IEqualityComparer<ComfySampler> Comparer { get; } = new NameEqualityComparer();
}
