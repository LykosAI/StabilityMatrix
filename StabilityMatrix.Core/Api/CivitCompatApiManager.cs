using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Api;

/// <summary>
/// Provides a compatibility layer for interacting with Civita APIs and Discovery APIs.
/// This class decides dynamically whether to use the Civita API or an alternative Discovery API
/// based on internal conditions.
/// </summary>
[RegisterSingleton<CivitCompatApiManager>]
public class CivitCompatApiManager(
    ILogger<CivitCompatApiManager> logger,
    ICivitApi civitApi,
    ILykosModelDiscoveryApi discoveryApi,
    ISettingsManager settingsManager
) : ICivitApi
{
    private bool ShouldUseDiscoveryApi => settingsManager.Settings.CivitUseDiscoveryApi;

    public Task<CivitModelsResponse> GetModels(CivitModelsRequest request)
    {
        if (ShouldUseDiscoveryApi)
        {
            logger.LogInformation($"Using Discovery API for {nameof(GetModels)}");
            return discoveryApi.GetModels(request, transcodeAnimToImage: true, transcodeVideoToImage: true);
        }

        return civitApi.GetModels(request);
    }

    public Task<CivitModel> GetModelById(int id)
    {
        if (ShouldUseDiscoveryApi)
        {
            logger.LogInformation($"Using Discovery API for {nameof(GetModelById)}");
            return discoveryApi.GetModelById(id);
        }
        return civitApi.GetModelById(id);
    }

    public Task<CivitModelVersionResponse> GetModelVersionByHash(string hash)
    {
        return civitApi.GetModelVersionByHash(hash);
    }

    public Task<CivitModelVersion> GetModelVersionById(int id)
    {
        return civitApi.GetModelVersionById(id);
    }

    public Task<HttpResponseMessage> GetBaseModelList()
    {
        return civitApi.GetBaseModelList();
    }
}
