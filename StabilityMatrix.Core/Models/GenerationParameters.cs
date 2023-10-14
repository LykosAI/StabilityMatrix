using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Core.Models;

[JsonSerializable(typeof(GenerationParameters))]
public partial record GenerationParameters
{
    public string? PositivePrompt { get; set; }
    public string? NegativePrompt { get; set; }
    public int Steps { get; set; }
    public string? Sampler { get; set; }
    public double CfgScale { get; set; }
    public ulong Seed { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public string? ModelHash { get; set; }
    public string? ModelName { get; set; }

    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out GenerationParameters? generationParameters
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            generationParameters = null;
            return false;
        }

        var lines = text.Split('\n');

        if (lines.LastOrDefault() is not { } lastLine)
        {
            generationParameters = null;
            return false;
        }

        if (lastLine.StartsWith("Steps:") != true)
        {
            lines = text.Split("\r\n");
            lastLine = lines.LastOrDefault() ?? string.Empty;

            if (lastLine.StartsWith("Steps:") != true)
            {
                generationParameters = null;
                return false;
            }
        }

        // Join lines before last line, split at 'Negative prompt: '
        var joinedLines = string.Join("\n", lines[..^1]);

        var splitFirstPart = joinedLines.Split("Negative prompt: ");
        if (splitFirstPart.Length != 2)
        {
            generationParameters = null;
            return false;
        }

        var positivePrompt = splitFirstPart[0];
        var negativePrompt = splitFirstPart[1];

        // Parse last line
        var match = ParseLastLineRegex().Match(lastLine);
        if (!match.Success)
        {
            generationParameters = null;
            return false;
        }

        generationParameters = new GenerationParameters
        {
            PositivePrompt = positivePrompt,
            NegativePrompt = negativePrompt,
            Steps = int.Parse(match.Groups["Steps"].Value),
            Sampler = match.Groups["Sampler"].Value,
            CfgScale = double.Parse(match.Groups["CfgScale"].Value),
            Seed = ulong.Parse(match.Groups["Seed"].Value),
            Height = int.Parse(match.Groups["Height"].Value),
            Width = int.Parse(match.Groups["Width"].Value),
            ModelHash = match.Groups["ModelHash"].Value,
            ModelName = match.Groups["ModelName"].Value,
        };

        return true;
    }

    /// <summary>
    /// Converts current <see cref="Sampler"/> string to <see cref="ComfySampler"/> and <see cref="ComfyScheduler"/>.
    /// </summary>
    /// <returns></returns>
    public (ComfySampler sampler, ComfyScheduler scheduler)? GetComfySamplers()
    {
        if (Sampler is not { } source)
            return null;

        var scheduler = source switch
        {
            _ when source.Contains("Karras") => ComfyScheduler.Karras,
            _ when source.Contains("Exponential") => ComfyScheduler.Exponential,
            _ => ComfyScheduler.Normal,
        };

        var sampler = source switch
        {
            "LMS" => ComfySampler.LMS,
            "DDIM" => ComfySampler.DDIM,
            "UniPC" => ComfySampler.UniPC,
            "DPM fast" => ComfySampler.DpmFast,
            "DPM adaptive" => ComfySampler.DpmAdaptive,
            "Heun" => ComfySampler.Heun,
            _ when source.StartsWith("DPM2 a") => ComfySampler.Dpm2Ancestral,
            _ when source.StartsWith("DPM2") => ComfySampler.Dpm2,
            _ when source.StartsWith("DPM++ 2M SDE") => ComfySampler.Dpmpp2MSde,
            _ when source.StartsWith("DPM++ 2M") => ComfySampler.Dpmpp2M,
            _ when source.StartsWith("DPM++ 3M SDE") => ComfySampler.Dpmpp3MSde,
            _ when source.StartsWith("DPM++ 3M") => ComfySampler.Dpmpp3M,
            _ when source.StartsWith("DPM++ SDE") => ComfySampler.DpmppSde,
            _ when source.StartsWith("DPM++ 2S a") => ComfySampler.Dpmpp2SAncestral,
            _ => default
        };

        return (sampler, scheduler);
    }

    /// <summary>
    /// Return a sample parameters for UI preview
    /// </summary>
    public static GenerationParameters GetSample()
    {
        return new GenerationParameters
        {
            PositivePrompt = "(cat:1.2), by artist, detailed, [shaded]",
            NegativePrompt = "blurry, jpg artifacts",
            Steps = 30,
            CfgScale = 7,
            Width = 640,
            Height = 896,
            Seed = 124825529,
            ModelName = "ExampleMix7",
            ModelHash = "b899d188a1ac7356bfb9399b2277d5b21712aa360f8f9514fba6fcce021baff7",
            Sampler = "DPM++ 2M Karras"
        };
    }

    // Example: Steps: 30, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 2216407431, Size: 640x896, Model hash: eb2h052f91, Model: anime_v1
    [GeneratedRegex(
        """^Steps: (?<Steps>\d+), Sampler: (?<Sampler>.+?), CFG scale: (?<CfgScale>\d+(\.\d+)?), Seed: (?<Seed>\d+), Size: (?<Width>\d+)x(?<Height>\d+), Model hash: (?<ModelHash>.+?), Model: (?<ModelName>.+)$"""
    )]
    private static partial Regex ParseLastLineRegex();
}
