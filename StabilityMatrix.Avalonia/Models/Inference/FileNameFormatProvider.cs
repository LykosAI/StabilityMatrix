using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Data;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Models.Inference;

public partial class FileNameFormatProvider
{
    public GenerationParameters? GenerationParameters { get; init; }

    public InferenceProjectType? ProjectType { get; init; }

    public string? ProjectName { get; init; }

    private Dictionary<string, Func<string?>>? _substitutions;

    public Dictionary<string, Func<string?>> Substitutions =>
        _substitutions ??= new Dictionary<string, Func<string?>>
        {
            { "seed", () => GenerationParameters?.Seed.ToString() },
            { "prompt", () => GenerationParameters?.PositivePrompt },
            { "negative_prompt", () => GenerationParameters?.NegativePrompt },
            { "model_name", () => Path.GetFileNameWithoutExtension(GenerationParameters?.ModelName) },
            { "model_hash", () => GenerationParameters?.ModelHash },
            { "sampler", () => GenerationParameters?.Sampler },
            { "cfgscale", () => GenerationParameters?.CfgScale.ToString() },
            { "steps", () => GenerationParameters?.Steps.ToString() },
            { "width", () => GenerationParameters?.Width.ToString() },
            { "height", () => GenerationParameters?.Height.ToString() },
            { "project_type", () => ProjectType?.GetStringValue() },
            { "project_name", () => ProjectName },
            { "date", () => DateTime.Now.ToString("yyyy-MM-dd") },
            { "time", () => DateTime.Now.ToString("HH-mm-ss") }
        };

    /// <summary>
    /// Validate a format string
    /// </summary>
    /// <param name="format">Format string</param>
    /// <exception cref="DataValidationException">Thrown if the format string contains an unknown variable</exception>
    [Pure]
    public ValidationResult Validate(string format)
    {
        var regex = BracketRegex();
        var matches = regex.Matches(format);
        var variables = matches.Select(m => m.Groups[1].Value);

        foreach (var variableText in variables)
        {
            try
            {
                var (variable, _) = ExtractVariableAndSlice(variableText);

                if (!Substitutions.ContainsKey(variable))
                {
                    return new ValidationResult($"Unknown variable '{variable}'");
                }
            }
            catch (Exception e)
            {
                return new ValidationResult($"Invalid variable '{variableText}': {e.Message}");
            }
        }

        return ValidationResult.Success!;
    }

    public IEnumerable<FileNameFormatPart> GetParts(string template)
    {
        var regex = BracketRegex();
        var matches = regex.Matches(template);

        var parts = new List<FileNameFormatPart>();

        // Loop through all parts of the string, including matches and non-matches
        var currentIndex = 0;

        foreach (var result in matches.Cast<Match>())
        {
            // If the match is not at the start of the string, add a constant part
            if (result.Index != currentIndex)
            {
                var constant = template[currentIndex..result.Index];
                parts.Add(constant);

                currentIndex += constant.Length;
            }

            // Now we're at start of the current match, add the variable part
            var (variable, slice) = ExtractVariableAndSlice(result.Groups[1].Value);
            var substitution = Substitutions[variable];

            // Slice string if necessary
            if (slice is not null)
            {
                parts.Add(
                    (FileNameFormatPart)(
                        () =>
                        {
                            var value = substitution();
                            if (value is null)
                                return null;

                            if (slice.End is null)
                            {
                                value = value[(slice.Start ?? 0)..];
                            }
                            else
                            {
                                var length = Math.Min(value.Length, slice.End.Value) - (slice.Start ?? 0);
                                value = value.Substring(slice.Start ?? 0, length);
                            }

                            return value;
                        }
                    )
                );
            }
            else
            {
                parts.Add(substitution);
            }

            currentIndex += result.Length;
        }

        // Add remaining as constant
        if (currentIndex != template.Length)
        {
            var constant = template[currentIndex..];
            parts.Add(constant);
        }

        return parts;
    }

    /// <summary>
    /// Return a sample provider for UI preview
    /// </summary>
    public static FileNameFormatProvider GetSample()
    {
        return new FileNameFormatProvider
        {
            GenerationParameters = GenerationParameters.GetSample(),
            ProjectType = InferenceProjectType.TextToImage,
            ProjectName = "Sample Project"
        };
    }

    /// <summary>
    /// Extract variable and index from a combined string
    /// </summary>
    private static (string Variable, Slice? Slice) ExtractVariableAndSlice(string combined)
    {
        if (IndexRegex().Matches(combined).FirstOrDefault() is not { Success: true } match)
        {
            return (combined, null);
        }

        // Variable is everything before the match
        var variable = combined[..match.Groups[0].Index];

        var start = match.Groups["start"].Value;
        var end = match.Groups["end"].Value;
        var step = match.Groups["step"].Value;

        var slice = new Slice(
            string.IsNullOrEmpty(start) ? null : int.Parse(start),
            string.IsNullOrEmpty(end) ? null : int.Parse(end),
            string.IsNullOrEmpty(step) ? null : int.Parse(step)
        );

        return (variable, slice);
    }

    /// <summary>
    /// Regex for matching contents within a curly brace.
    /// </summary>
    [GeneratedRegex(@"\{([a-z_:\d\[\]]+)\}")]
    private static partial Regex BracketRegex();

    /// <summary>
    /// Regex for matching a Python-like array index.
    /// </summary>
    [GeneratedRegex(@"\[(?:(?<start>-?\d+)?)\:(?:(?<end>-?\d+)?)?(?:\:(?<step>-?\d+))?\]")]
    private static partial Regex IndexRegex();

    private record Slice(int? Start, int? End, int? Step);
}
