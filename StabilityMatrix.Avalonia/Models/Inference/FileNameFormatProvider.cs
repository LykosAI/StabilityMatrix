using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Data;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

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
            { "model_name", () => GenerationParameters?.ModelName },
            { "model_hash", () => GenerationParameters?.ModelHash },
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

        foreach (var variable in variables)
        {
            if (!Substitutions.ContainsKey(variable))
            {
                return new ValidationResult($"Unknown variable '{variable}'");
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
                parts.Add(FileNameFormatPart.FromConstant(constant));

                currentIndex += constant.Length;
            }

            // Now we're at start of the current match, add the variable part
            var variable = result.Groups[1].Value;

            parts.Add(FileNameFormatPart.FromSubstitution(Substitutions[variable]));

            currentIndex += result.Length;
        }

        // Add remaining as constant
        if (currentIndex != template.Length)
        {
            var constant = template[currentIndex..];
            parts.Add(FileNameFormatPart.FromConstant(constant));
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
    /// Regex for matching contents within a curly brace.
    /// </summary>
    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex BracketRegex();

    /// <summary>
    /// Regex for matching a Python-like array index.
    /// </summary>
    [GeneratedRegex(@"\[(?:(?<start>-?\d+)?)\:(?:(?<end>-?\d+)?)?(?:\:(?<step>-?\d+))?\]")]
    private static partial Regex IndexRegex();
}
