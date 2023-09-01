using System.Collections.Immutable;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.Comfy;

[JsonConverter(typeof(StringJsonConverter<ComfySampler>))]
public readonly record struct ComfySampler(string Name)
{
    private static Dictionary<string, string> ConvertDict { get; } =
        new()
        {
            ["euler"] = "Euler",
            ["euler_ancestral"] = "Euler Ancestral",
            ["heun"] = "Heun",
            ["dpm_2"] = "DPM 2",
            ["dpm_2_ancestral"] = "DPM 2 Ancestral",
            ["lms"] = "LMS",
            ["dpm_fast"] = "DPM Fast",
            ["dpm_adaptive"] = "DPM Adaptive",
            ["dpmpp_2s_ancestral"] = "DPM++ 2S Ancestral",
            ["dpmpp_sde"] = "DPM++ SDE",
            ["dpmpp_sde_gpu"] = "DPM++ SDE GPU",
            ["dpmpp_2m"] = "DPM++ 2M",
            ["dpmpp_2m_sde"] = "DPM++ 2M SDE",
            ["dpmpp_2m_sde_gpu"] = "DPM++ 2M SDE GPU",
            ["dpmpp_3m"] = "DPM++ 3M",
            ["dpmpp_3m_sde"] = "DPM++ 3M SDE",
            ["dpmpp_3m_sde_gpu"] = "DPM++ 3M SDE GPU",
            ["ddim"] = "DDIM",
            ["uni_pc"] = "UniPC",
            ["uni_pc_bh2"] = "UniPC BH2"
        };

    public static IReadOnlyList<ComfySampler> Defaults { get; } =
        ConvertDict.Keys.Select(k => new ComfySampler(k)).ToImmutableArray();

    public string DisplayName =>
        ConvertDict.TryGetValue(Name, out var displayName) ? displayName : Name;

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
