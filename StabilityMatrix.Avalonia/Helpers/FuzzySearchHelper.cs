using System;
using FuzzySharp;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Shared fuzzy text matching for model search boxes.
/// </summary>
public static class FuzzySearchHelper
{
    /// <summary>
    /// Minimum fuzzy score (0-100) for a candidate to be accepted when it does not
    /// contain the query as a substring.
    /// </summary>
    public const int ScoreThreshold = 70;

    /// <summary>
    /// Scores <paramref name="candidateText"/> against <paramref name="query"/>.
    /// Callers that only need accept/reject can use <see cref="FuzzyMatchResult.IsMatch"/>;
    /// callers that rank results can order by <see cref="FuzzyMatchResult.ContainsQuery"/>
    /// then <see cref="FuzzyMatchResult.Score"/>.
    /// </summary>
    public static FuzzyMatchResult Match(string query, string candidateText)
    {
        var containsQuery = candidateText.Contains(query, StringComparison.OrdinalIgnoreCase);
        var score = Math.Max(
            Fuzz.WeightedRatio(query, candidateText),
            Fuzz.PartialRatio(query, candidateText)
        );
        return new FuzzyMatchResult(containsQuery, score);
    }

    /// <summary>
    /// Whether <paramref name="candidateText"/> is accepted for <paramref name="query"/>.
    /// </summary>
    public static bool IsMatch(string query, string candidateText) => Match(query, candidateText).IsMatch;
}

/// <summary>
/// Result of matching a search query against a candidate's searchable text.
/// </summary>
/// <param name="ContainsQuery">Whether the candidate text contains the query as a case-insensitive substring.</param>
/// <param name="Score">Fuzzy similarity score (0-100).</param>
public readonly record struct FuzzyMatchResult(bool ContainsQuery, int Score)
{
    /// <summary>
    /// Whether the candidate is accepted: a substring hit, or a score at or above
    /// <see cref="FuzzySearchHelper.ScoreThreshold"/>.
    /// </summary>
    public bool IsMatch => ContainsQuery || Score >= FuzzySearchHelper.ScoreThreshold;
}
