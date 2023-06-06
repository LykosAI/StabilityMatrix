using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Api;

public interface ICivitApi
{
    [Get("/api/v1/models")]
    Task<CivitModelsResponse> GetModels(CivitModelsRequest request);
    
    [Get("/api/v1/model-versions/by-hash")]
    Task<CivitModelVersion> GetModelVersionByHash([Query] string hash);
}
