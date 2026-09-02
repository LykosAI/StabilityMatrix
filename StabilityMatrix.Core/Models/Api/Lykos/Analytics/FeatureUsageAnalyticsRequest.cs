namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class FeatureUsageAnalyticsRequest : UsageAnalyticsRequest
{
    /// <summary>Visit count per app page (e.g. Inference, CheckpointBrowser).</summary>
    public IReadOnlyDictionary<string, int> PageVisits { get; set; } = new Dictionary<string, int>();
}
