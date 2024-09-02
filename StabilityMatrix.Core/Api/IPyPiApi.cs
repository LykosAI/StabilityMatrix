using Refit;
using StabilityMatrix.Core.Models.Api.Pypi;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix/2.x")]
public interface IPyPiApi
{
    [Get("/pypi/{packageName}/json")]
    Task<PyPiResponse> GetPackageInfo(string packageName);
}
