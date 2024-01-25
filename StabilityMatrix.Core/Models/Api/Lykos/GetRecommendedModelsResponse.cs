namespace StabilityMatrix.Core.Models.Api.Lykos;

public class GetRecommendedModelsResponse
{
    public required ModelLists Sd15 { get; set; }
    public required ModelLists Sdxl { get; set; }
    public required ModelLists Decoders { get; set; }
}

public class ModelLists
{
    public IEnumerable<int>? CivitAi { get; set; }
    public IEnumerable<string>? HuggingFace { get; set; }
}
