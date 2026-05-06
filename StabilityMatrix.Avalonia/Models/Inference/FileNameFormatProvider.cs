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
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Models.Inference;

public partial class FileNameFormatProvider
{
    public GenerationParameters? GenerationParameters { get; init; }

    public InferenceProjectType? ProjectType { get; init; }

    public string? ProjectName { get; init; }

    public CivitModel? CivitModel { get; init; }
    public CivitModelVersion? CivitModelVersion { get; init; }
    public CivitFile? CivitFile { get; init; }
    public LocalModelFile? LocalModelFile { get; init; }

    public static ISet<string> LocalOrganizationVariables { get; } =
        new HashSet<string>(
            [
                "date",
                "time",
                "author",
                "base_model",
                "file_name",
                "file_id",
                "model_id",
                "model_name",
                "model_version_id",
                "model_version_name",
                "model_type",
            ],
            StringComparer.Ordinal
        );

    private Dictionary<string, Func<string?>>? _substitutions;

    public Dictionary<string, Func<string?>> Substitutions =>
        _substitutions ??= new Dictionary<string, Func<string?>>
        {
            { "seed", () => GenerationParameters?.Seed.ToString() },
            { "prompt", () => GenerationParameters?.PositivePrompt },
            { "negative_prompt", () => GenerationParameters?.NegativePrompt },
            {
                "model_name",
                () =>
                    Path.GetFileNameWithoutExtension(GenerationParameters?.ModelName)
                    ?? CivitModel?.Name
                    ?? LocalModelFile?.ConnectedModelInfo?.ModelName
                    ?? LocalModelFile?.FileNameWithoutExtension
            },
            { "model_hash", () => GenerationParameters?.ModelHash },
            { "sampler", () => GenerationParameters?.Sampler },
            { "cfgscale", () => GenerationParameters?.CfgScale.ToString() },
            { "steps", () => GenerationParameters?.Steps.ToString() },
            { "width", () => GenerationParameters?.Width.ToString() },
            { "height", () => GenerationParameters?.Height.ToString() },
            { "project_type", () => ProjectType?.GetStringValue() },
            { "project_name", () => ProjectName },
            { "date", () => DateTime.Now.ToString("yyyy-MM-dd") },
            { "time", () => DateTime.Now.ToString("HH-mm-ss") },
            {
                "author",
                () => CivitModel?.Creator?.Username ?? LocalModelFile?.ConnectedModelInfo?.AuthorUsername
            },
            {
                "base_model",
                () => CivitModelVersion?.BaseModel ?? LocalModelFile?.ConnectedModelInfo?.BaseModel
            },
            {
                "file_name",
                () =>
                    Path.GetFileNameWithoutExtension(CivitFile?.Name)
                    ?? Path.GetFileNameWithoutExtension(LocalModelFile?.ConnectedModelInfo?.RemoteFileName)
                    ?? LocalModelFile?.FileNameWithoutExtension
            },
            {
                "file_id",
                () => CivitFile?.Id.ToString() ?? LocalModelFile?.ConnectedModelInfo?.RemoteFileId?.ToString()
            },
            {
                "model_id",
                () => CivitModel?.Id.ToString() ?? LocalModelFile?.ConnectedModelInfo?.ModelId?.ToString()
            },
            {
                "model_version_id",
                () =>
                    CivitModelVersion?.Id.ToString()
                    ?? LocalModelFile?.ConnectedModelInfo?.VersionId?.ToString()
            },
            {
                "model_version_name",
                () => CivitModelVersion?.Name ?? LocalModelFile?.ConnectedModelInfo?.VersionName
            },
            {
                "model_type",
                () =>
                    CivitModel?.Type.ToString()
                    ?? (LocalModelFile?.ConnectedModelInfo is { } cmInfo ? cmInfo.ModelType.ToString() : null)
            },
        };

    /// <summary>
    /// Validate a format string
    /// </summary>
    /// <param name="format">Format string</param>
    /// <exception cref="DataValidationException">Thrown if the format string contains an unknown variable</exception>
    [Pure]
    public ValidationResult Validate(string format)
    {
        var variables = GetVariableTexts(format);

        foreach (var variableText in variables)
        {
            try
            {
                var variable = GetVariableName(variableText);

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

    public IEnumerable<string> GetVariableTexts(string template)
    {
        return VariableRegex().Matches(template).Select(m => m.Groups[1].Value);
    }

    public string GetVariableName(string variableText)
    {
        var (variable, _) = ExtractVariableAndSlice(variableText);
        return variable;
    }

    public bool TryResolveVariable(string variableText, out string? value, out string? error)
    {
        try
        {
            var (variable, slice) = ExtractVariableAndSlice(variableText);
            if (!Substitutions.TryGetValue(variable, out var substitution))
            {
                value = null;
                error = $"Unknown variable '{variable}'";
                return false;
            }

            value = ApplySlice(substitution(), slice);
            if (value is null)
            {
                error = $"Variable '{variable}' is not available";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception e)
        {
            value = null;
            error = $"Invalid variable '{variableText}': {e.Message}";
            return false;
        }
    }

    public IEnumerable<FileNameFormatPart> GetParts(string template)
    {
        var regex = VariableRegex();
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

            parts.Add((FileNameFormatPart)(() => ApplySlice(substitution(), slice)));

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
            ProjectName = "Sample Project",
        };
    }

    public static FileNameFormatProvider GetSampleForModelBrowser()
    {
        return new FileNameFormatProvider
        {
            CivitModel = new CivitModel
            {
                Id = 1234,
                Name = "Sample Model",
                Creator = new CivitCreator { Username = "SampleUser" },
                Type = CivitModelType.Checkpoint,
            },
            CivitModelVersion = new CivitModelVersion
            {
                Id = 5678,
                Name = "v1.0",
                BaseModel = "Illustrious",
            },
            CivitFile = new CivitFile
            {
                Id = 910,
                Name = "sample_file.ckpt",
                Type = CivitFileType.Model,
                Metadata = new CivitFileMetadata { Size = "pruned" },
            },
        };
    }

    public static FileNameFormatProvider GetSampleForOrganization()
    {
        return new FileNameFormatProvider
        {
            LocalModelFile = new LocalModelFile
            {
                RelativePath = "StableDiffusion/sample_file.safetensors",
                SharedFolderType = SharedFolderType.StableDiffusion,
                ConnectedModelInfo = new ConnectedModelInfo
                {
                    ModelId = 1234,
                    ModelName = "Sample Model",
                    ModelDescription = string.Empty,
                    Nsfw = false,
                    Tags = [],
                    ModelType = CivitModelType.Checkpoint,
                    VersionId = 5678,
                    VersionName = "v1.0",
                    AuthorUsername = "SampleUser",
                    BaseModel = "Illustrious",
                    RemoteFileName = "sample_file.safetensors",
                    RemoteFileId = 910,
                    Hashes = new CivitFileHashes(),
                    Source = ConnectedModelSource.Civitai,
                },
            },
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

    private static string? ApplySlice(string? value, Slice? slice)
    {
        if (value is null || slice is null)
            return value;

        if (slice.End is null)
        {
            return value[(slice.Start ?? 0)..];
        }

        var length = Math.Min(value.Length, slice.End.Value) - (slice.Start ?? 0);
        return value.Substring(slice.Start ?? 0, length);
    }

    /// <summary>
    /// Regex for matching contents within a curly brace.
    /// </summary>
    [GeneratedRegex(@"\{([a-z_:\d\[\]]+)\}")]
    private static partial Regex BracketRegex();

    internal static Regex VariableRegex() => BracketRegex();

    /// <summary>
    /// Regex for matching a Python-like array index.
    /// </summary>
    [GeneratedRegex(@"\[(?:(?<start>-?\d+)?)\:(?:(?<end>-?\d+)?)?(?:\:(?<step>-?\d+))?\]")]
    private static partial Regex IndexRegex();

    private record Slice(int? Start, int? End, int? Step);
}
