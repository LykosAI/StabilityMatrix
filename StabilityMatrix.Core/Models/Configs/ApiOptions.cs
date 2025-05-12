namespace StabilityMatrix.Core.Models.Configs;

/// <summary>
/// Configuration options for API services
/// </summary>
public record ApiOptions
{
    /// <summary>
    /// Base URL for Lykos Authentication API
    /// </summary>
    public Uri AuthApiBaseUrl { get; init; } = new("https://auth.lykos.ai");

    /// <summary>
    /// Base URL for Lykos Analytics API
    /// </summary>
    public Uri AnalyticsApiBaseUrl { get; init; } = new("https://analytics.lykos.ai");

    /// <summary>
    /// Base URL for Lykos Account API
    /// </summary>
    public Uri AccountApiBaseUrl { get; init; } = new("https://account.lykos.ai/");

    /// <summary>
    /// Base URL for PromptGen API
    /// </summary>
    public Uri PromptGenApiBaseUrl { get; init; } = new("https://promptgen.lykos.ai/api");
}
