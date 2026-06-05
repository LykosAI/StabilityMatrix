using FuzzySharp;
using FuzzySharp.PreProcess;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.Helpers;

public class ImageSearcher
{
    public int MinimumFuzzScore { get; init; } = 80;

    public ImageSearchOptions SearchOptions { get; init; } = ImageSearchOptions.All;

    public Func<LocalImageFile, bool> GetPredicate(string? searchQuery)
    {
        if (string.IsNullOrEmpty(searchQuery))
        {
            return _ => true;
        }

        return file =>
        {
            if (file.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (
                SearchOptions.HasFlag(ImageSearchOptions.FileName)
                && Fuzz.WeightedRatio(searchQuery, file.FileName, PreprocessMode.Full) > MinimumFuzzScore
            )
            {
                return true;
            }

            // Generation params
            if (file.GenerationParameters is { } parameters)
            {
                if (
                    SearchOptions.HasFlag(ImageSearchOptions.PositivePrompt)
                        && (
                            parameters.PositivePrompt?.Contains(
                                searchQuery,
                                StringComparison.OrdinalIgnoreCase
                            ) ?? false
                        )
                    || SearchOptions.HasFlag(ImageSearchOptions.NegativePrompt)
                        && (
                            parameters.NegativePrompt?.Contains(
                                searchQuery,
                                StringComparison.OrdinalIgnoreCase
                            ) ?? false
                        )
                    || SearchOptions.HasFlag(ImageSearchOptions.Seed)
                        && parameters
                            .Seed.ToString()
                            .StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase)
                    || SearchOptions.HasFlag(ImageSearchOptions.Sampler)
                        && (
                            parameters.Sampler?.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase)
                            ?? false
                        )
                    || SearchOptions.HasFlag(ImageSearchOptions.ModelName)
                        && (
                            parameters.ModelName?.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase)
                            ?? false
                        )
                )
                {
                    return true;
                }
            }

            return false;
        };
    }

    [Flags]
    public enum ImageSearchOptions
    {
        None = 0,
        FileName = 1 << 0,
        PositivePrompt = 1 << 1,
        NegativePrompt = 1 << 2,
        Seed = 1 << 3,
        Sampler = 1 << 4,
        ModelName = 1 << 5,
        All = int.MaxValue,
    }
}
