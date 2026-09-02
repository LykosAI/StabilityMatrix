namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class FeatureUsageAnalyticsRequest : UsageAnalyticsRequest
{
    public override string Type { get; set; } = "feature-usage";

    /// <summary>Visit count per app page (e.g. Inference, CheckpointBrowser).</summary>
    public IReadOnlyDictionary<string, int> PageVisits { get; set; } = new Dictionary<string, int>();
}
