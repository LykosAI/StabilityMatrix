namespace StabilityMatrix.Core.Models.Api.Lykos;

public class GetRecommendedModelsResponse
{
    public required IEnumerable<int> Sd15 { get; set; }
    public required IEnumerable<int> Sdxl { get; set; }
}
