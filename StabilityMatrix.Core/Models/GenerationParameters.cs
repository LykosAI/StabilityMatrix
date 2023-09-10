using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models;

[JsonSerializable(typeof(GenerationParameters))]
public partial class GenerationParameters
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
        string text,
        [NotNullWhen(true)] out GenerationParameters? generationParameters
    )
    {
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
            ModelHash = match.Groups["ModelHash"].Value,
            ModelName = match.Groups["ModelName"].Value,
        };

        return true;
    }

    [GeneratedRegex(
        """^Steps: (?<Steps>\d+), Sampler: (?<Sampler>.+?), CFG scale: (?<CfgScale>\d+(\.\d+)?), Seed: (?<Seed>\d+), Size: \d+x\d+, Model hash: (?<ModelHash>.+?), Model: (?<ModelName>.+)$"""
    )]
    private static partial Regex ParseLastLineRegex();
}
