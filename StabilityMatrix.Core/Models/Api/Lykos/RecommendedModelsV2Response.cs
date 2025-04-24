namespace StabilityMatrix.Core.Models.Api.Lykos;

public class RecommendedModelsV2Response
{
    public Dictionary<string, List<CivitModel>> RecommendedModelsByCategory { get; set; } = new();
}
