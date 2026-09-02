namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class PackageUsageAnalyticsRequest : UsageAnalyticsRequest
{
    /// <summary>Launch count per package name.</summary>
    public IReadOnlyDictionary<string, int> Launches { get; set; } = new Dictionary<string, int>();

    /// <summary>Total minutes running per package name.</summary>
    public IReadOnlyDictionary<string, int> RunMinutes { get; set; } = new Dictionary<string, int>();
}
