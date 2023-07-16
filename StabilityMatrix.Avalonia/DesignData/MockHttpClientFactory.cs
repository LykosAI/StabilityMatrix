using System;
using System.Net.Http;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        throw new NotImplementedException();
    }
}
