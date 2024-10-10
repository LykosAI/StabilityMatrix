using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Core.Helper;

public static class GenerationParametersConverter
{
    private static readonly ImmutableDictionary<string, ComfySamplerScheduler> ParamsToSamplerSchedulers =
        new Dictionary<string, ComfySamplerScheduler>
        {
            ["DPM++ 2M Karras"] = (ComfySampler.Dpmpp2M, ComfyScheduler.Karras),
            ["DPM++ SDE Karras"] = (ComfySampler.DpmppSde, ComfyScheduler.Karras),
            ["DPM++ 2M SDE Exponential"] = (ComfySampler.Dpmpp2MSde, ComfyScheduler.Exponential),
            ["DPM++ 2M SDE Karras"] = (ComfySampler.Dpmpp2MSde, ComfyScheduler.Karras),
            ["Euler a"] = (ComfySampler.EulerAncestral, ComfyScheduler.Normal),
            ["Euler"] = (ComfySampler.Euler, ComfyScheduler.Normal),
            ["Euler Simple"] = (ComfySampler.Euler, ComfyScheduler.Simple),
            ["LMS"] = (ComfySampler.LMS, ComfyScheduler.Normal),
            ["Heun"] = (ComfySampler.Heun, ComfyScheduler.Normal),
            ["Heun Beta"] = (ComfySampler.Heun, ComfyScheduler.Beta),
            ["DPM2"] = (ComfySampler.Dpm2, ComfyScheduler.Normal),
            ["DPM2 Karras"] = (ComfySampler.Dpm2, ComfyScheduler.Karras),
            ["DPM2 a"] = (ComfySampler.Dpm2Ancestral, ComfyScheduler.Normal),
            ["DPM2 a Karras"] = (ComfySampler.Dpm2Ancestral, ComfyScheduler.Karras),
            ["DPM++ 2S a"] = (ComfySampler.Dpmpp2SAncestral, ComfyScheduler.Normal),
            ["DPM++ 2S a Karras"] = (ComfySampler.Dpmpp2SAncestral, ComfyScheduler.Karras),
            ["DPM++ 2M"] = (ComfySampler.Dpmpp2M, ComfyScheduler.Normal),
            ["DPM++ SDE"] = (ComfySampler.DpmppSde, ComfyScheduler.Normal),
            ["DPM++ 2M SDE"] = (ComfySampler.Dpmpp2MSde, ComfyScheduler.Normal),
            ["DPM++ 3M SDE"] = (ComfySampler.Dpmpp3MSde, ComfyScheduler.Normal),
            ["DPM++ 3M SDE Karras"] = (ComfySampler.Dpmpp3MSde, ComfyScheduler.Karras),
            ["DPM++ 3M SDE Exponential"] = (ComfySampler.Dpmpp3MSde, ComfyScheduler.Exponential),
            ["DPM fast"] = (ComfySampler.DpmFast, ComfyScheduler.Normal),
            ["DPM adaptive"] = (ComfySampler.DpmAdaptive, ComfyScheduler.Normal),
            ["LMS Karras"] = (ComfySampler.LMS, ComfyScheduler.Karras),
            ["DDIM"] = (ComfySampler.DDIM, ComfyScheduler.Normal),
            ["DDIM Beta"] = (ComfySampler.DDIM, ComfyScheduler.Beta),
            ["UniPC"] = (ComfySampler.UniPC, ComfyScheduler.Normal),
        }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<ComfySamplerScheduler, string> SamplerSchedulersToParams =
        ParamsToSamplerSchedulers.ToImmutableDictionary(x => x.Value, x => x.Key);

    /// <summary>
    /// Converts a parameters-type string to a <see cref="ComfySamplerScheduler"/>.
    /// </summary>
    public static bool TryGetSamplerScheduler(string parameters, out ComfySamplerScheduler samplerScheduler)
    {
        return ParamsToSamplerSchedulers.TryGetValue(parameters, out samplerScheduler);
    }

    /// <summary>
    /// Converts a <see cref="ComfySamplerScheduler"/> to a parameters-type string.
    /// </summary>
    public static bool TryGetParameters(
        ComfySamplerScheduler samplerScheduler,
        [NotNullWhen(true)] out string? parameters
    )
    {
        return SamplerSchedulersToParams.TryGetValue(samplerScheduler, out parameters);
    }
}
