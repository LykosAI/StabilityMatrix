using Refit;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Api.LykosAuthApi;

public interface IRecommendedModelsApi
{
    [Get("/api/v2/Models/recommended")]
    Task<RecommendedModelsV2Response> GetRecommendedModels();
}
