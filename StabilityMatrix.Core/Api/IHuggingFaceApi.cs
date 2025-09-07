using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Core.Models.Api.HuggingFace;

namespace StabilityMatrix.Core.Api;

public interface IHuggingFaceApi
{
    [Get("/api/whoami-v2")]
    Task<IApiResponse<HuggingFaceUser>> GetCurrentUserAsync([Header("Authorization")] string authorization);
}
