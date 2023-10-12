using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.Inference;

public partial class FileNameFormatProvider
{
    public GenerationParameters? GenerationParameters { get; init; }

    public InferenceProjectType? ProjectType { get; init; }

    public string? ProjectName { get; init; }

    private Dictionary<string, Func<string?>>? _substitutions;

    private Dictionary<string, Func<string?>> Substitutions =>
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

    public (int Current, int Total)? BatchInfo { get; init; }

    /// <summary>
    /// Validate a format string
    /// </summary>
    public void Validate(string format)
    {
        var regex = BracketRegex();
        var matches = regex.Matches(format);
        var variables = matches.Select(m => m.Value[1..^1]).ToList();

        foreach (var variable in variables)
        {
            if (!Substitutions.ContainsKey(variable))
            {
                throw new ArgumentException($"Unknown variable '{variable}'");
            }
        }
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
                parts.Add(new FileNameFormatPart(constant, null));

                currentIndex += constant.Length;
            }

            var variable = result.Value[1..^1];
            parts.Add(new FileNameFormatPart(null, Substitutions[variable]));

            currentIndex += result.Length;
        }

        // Add remaining as constant
        if (currentIndex != template.Length)
        {
            var constant = template[currentIndex..];
            parts.Add(new FileNameFormatPart(constant, null));
        }

        return parts;
    }

    /// <summary>
    /// Return a string substituting the variables in the format string
    /// </summary>
    private string? GetSubstitution(string variable)
    {
        return variable switch
        {
            "seed" => GenerationParameters.Seed.ToString(),
            "model_name" => GenerationParameters.ModelName,
            "model_hash" => GenerationParameters.ModelHash,
            "width" => GenerationParameters.Width.ToString(),
            "height" => GenerationParameters.Height.ToString(),
            "date" => DateTime.Now.ToString("yyyy-MM-dd"),
            "time" => DateTime.Now.ToString("HH-mm-ss"),
            _ => throw new ArgumentOutOfRangeException(nameof(variable), variable, null)
        };
    }

    [GeneratedRegex(@"\{[a-z_]+\}")]
    private static partial Regex BracketRegex();
}
