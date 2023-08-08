namespace StabilityMatrix.Core.Models.Api.Comfy;

public readonly record struct ComfySampler(string Name)
{
    ///     SAMPLERS = ["euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral",
    /// "lms", "dpm_fast", "dpm_adaptive", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_sde_gpu",
    /// "dpmpp_2m", "dpmpp_2m_sde", "dpmpp_2m_sde_gpu", "ddim", "uni_pc", "uni_pc_bh2"]
    private static Dictionary<string, string> ConvertDict { get; } = new()
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
        ["ddim"] = "DDIM",
        ["uni_pc"] = "UniPC",
        ["uni_pc_bh2"] = "UniPC BH2"
    };
    
    public string DisplayName => ConvertDict.TryGetValue(Name, out var displayName) ? displayName : Name;
}
