using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Recognizes few-step "lightning" / turbo / distill LoRAs by filename, so the video model
/// card can nudge toward the CFG 1 + low-step sampling they need (running them at a normal
/// CFG produces oversaturated noise).
/// </summary>
public static partial class LightningLora
{
    [GeneratedRegex(@"(\d{1,2})step", RegexOptions.IgnoreCase)]
    private static partial Regex StepCountRegex();

    /// <summary>
    /// Whether the filename looks like a few-step speed LoRA. <paramref name="steps"/> is the
    /// step count when the name states one (e.g. "4steps", "8step"), otherwise null.
    /// </summary>
    public static bool TryDetect(string fileName, out int? steps)
    {
        var normalized = string.Concat(
            fileName.ToLowerInvariant().Where(c => c is not ('.' or '_' or '-' or ' '))
        );

        steps = null;
        if (
            StepCountRegex().Match(normalized) is { Success: true } match
            && int.TryParse(match.Groups[1].Value, out var parsed)
            && parsed is >= 1 and <= 16
        )
        {
            steps = parsed;
        }

        return steps is not null
            || normalized.Contains("lightning", StringComparison.Ordinal)
            || normalized.Contains("lightx2v", StringComparison.Ordinal)
            || normalized.Contains("turbo", StringComparison.Ordinal);
    }
}
