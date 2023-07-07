using System;
using System.Net.Http;
using Refit;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Api;

public class A3WebApiManager : IA3WebApiManager
{
    private IA3WebApi? client;
    public IA3WebApi Client
    {
        get
        {
            // Return the existing client if it exists
            if (client != null)
            {
                return client;
            }
            // Create a new client and store it otherwise
            client = CreateClient();
            return client;
        }
    }
    
    private readonly ISettingsManager settingsManager;
    private readonly IHttpClientFactory httpClientFactory;
    public RefitSettings? RefitSettings { get; init; }
    public string? BaseUrl { get; set; }
    
    public A3WebApiManager(ISettingsManager settingsManager, IHttpClientFactory httpClientFactory)
    {
        this.settingsManager = settingsManager;
        this.httpClientFactory = httpClientFactory;
    }

    public void ResetClient()
    {
        client = null;
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

        var httpClient = httpClientFactory.CreateClient("A3Client");
        httpClient.BaseAddress = new Uri(BaseUrl);
        var api = RestService.For<IA3WebApi>(httpClient, RefitSettings);
        return api;
    }
}
