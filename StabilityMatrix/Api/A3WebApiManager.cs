using System;
using System.Net.Http;
using Polly.Retry;
using Refit;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Api;

public class A3WebApiManager : IA3WebApiManager
{
    private IA3WebApi? _client;
    public IA3WebApi Client
    {
        get
        {
            // Return the existing client if it exists
            if (_client != null)
            {
                return _client;
            }
            // Create a new client and store it otherwise
            _client = CreateClient();
            return _client;
        }
    }
    
    private readonly ISettingsManager settingsManager;
    public AsyncRetryPolicy<HttpResponseMessage>? RetryPolicy { get; init; }
    public RefitSettings? RefitSettings { get; init; }
    public string? BaseUrl { get; set; }
    
    public A3WebApiManager(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
    }

    public void ResetClient()
    {
        _client = null;
    }

    private IA3WebApi CreateClient()
    {
        var settings = settingsManager.Settings;
        
        // First check override
        if (settings.WebApiHost != null)
        {
            BaseUrl = settings.WebApiHost;

            if (settings.WebApiPort != null)
            {
                BaseUrl += $":{settings.WebApiPort}";
            }
        }
        else
        {
            // Otherwise use default
            BaseUrl = "http://localhost:7860";
        }

        var client = RestService.For<IA3WebApi>(BaseUrl, RefitSettings);
        return client;
    }
}
