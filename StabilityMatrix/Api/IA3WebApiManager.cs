using System.Net.Http;
using Polly.Retry;
using Refit;

namespace StabilityMatrix.Api;

public interface IA3WebApiManager
{
    IA3WebApi Client { get; }
    RefitSettings? RefitSettings { get; init; }
    string? BaseUrl { get; set; }
    void ResetClient();
}
