namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class InferenceUsageAnalyticsRequest : UsageAnalyticsRequest
{
    public override string Type { get; set; } = "inference-usage";

    /// <summary>Generation count per Inference tab type (e.g. TextToImage, Upscale).</summary>
    public IReadOnlyDictionary<string, int> Generations { get; set; } = new Dictionary<string, int>();

    /// <summary>Generation count per model family (workflow profile key, e.g. Flux, ZImageTurbo).</summary>
    public IReadOnlyDictionary<string, int> ModelFamilies { get; set; } = new Dictionary<string, int>();
}
