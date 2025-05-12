namespace StabilityMatrix.Core.Models.Configs;

/// <summary>
/// Configuration options for API services
/// </summary>
public record ApiOptions
{
    /// <summary>
    /// Base URL for Lykos Authentication API
    /// </summary>
    public Uri LykosAuthApiBaseUrl { get; init; } = new("https://auth.lykos.ai");

    /// <summary>
    /// Base URL for Lykos Analytics API
    /// </summary>
    public Uri LykosAnalyticsApiBaseUrl { get; init; } = new("https://analytics.lykos.ai");

    /// <summary>
    /// Base URL for Lykos Account API
    /// </summary>
    public Uri LykosAccountApiBaseUrl { get; init; } = new("https://account.lykos.ai/");

    /// <summary>
    /// Base URL for PromptGen API
    /// </summary>
    public Uri LykosPromptGenApiBaseUrl { get; init; } = new("https://promptgen.lykos.ai/api");
}
