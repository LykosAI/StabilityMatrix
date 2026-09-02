namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class InferenceUsageAnalyticsRequest : UsageAnalyticsRequest
{
    /// <summary>Generation count per Inference tab type (e.g. TextToImage, Upscale).</summary>
    public IReadOnlyDictionary<string, int> Generations { get; set; } = new Dictionary<string, int>();

    /// <summary>New tab count per Inference tab type.</summary>
    public IReadOnlyDictionary<string, int> TabsOpened { get; set; } = new Dictionary<string, int>();
}
