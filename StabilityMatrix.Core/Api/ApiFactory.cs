using Refit;

namespace StabilityMatrix.Core.Api;

public class ApiFactory : IApiFactory
{
    private readonly IHttpClientFactory httpClientFactory;
    public RefitSettings? RefitSettings { get; init; }

    public ApiFactory(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public T CreateRefitClient<T>(Uri baseAddress)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(T));
        httpClient.BaseAddress = baseAddress;
        return RestService.For<T>(httpClient, RefitSettings);
    }

    public T CreateRefitClient<T>(Uri baseAddress, RefitSettings refitSettings)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(T));
        httpClient.BaseAddress = baseAddress;

        return RestService.For<T>(httpClient, refitSettings);
    }
}
