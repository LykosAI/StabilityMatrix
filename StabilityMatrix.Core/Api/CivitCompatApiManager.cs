using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Refit;
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
        return GetModelsInternal(request);
    }

    private async Task<CivitModelsResponse> GetModelsInternal(CivitModelsRequest request)
    {
        if (!ShouldUseDiscoveryApi)
        {
            return await civitApi.GetModels(request).ConfigureAwait(false);
        }

        try
        {
            logger.LogDebug("Using Discovery API for {Method}", nameof(GetModels));
            return await discoveryApi
                .GetModels(request, transcodeAnimToImage: true, transcodeVideoToImage: true)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
            when ((int)ex.StatusCode >= 500 || ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                ex,
                "Discovery API failed for {Method} with {StatusCode}; falling back to direct CivitAI API",
                nameof(GetModels),
                ex.StatusCode
            );
            return await civitApi.GetModels(request).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Discovery API request failed for {Method}; falling back to direct CivitAI API",
                nameof(GetModels)
            );
            return await civitApi.GetModels(request).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(
                ex,
                "Discovery API timed out for {Method}; falling back to direct CivitAI API",
                nameof(GetModels)
            );
            return await civitApi.GetModels(request).ConfigureAwait(false);
        }
    }

    public Task<CivitModel> GetModelById(int id)
    {
        /*if (ShouldUseDiscoveryApi)
        {
            logger.LogDebug($"Using Discovery API for {nameof(GetModelById)}");
            return discoveryApi.GetModelById(id);
        }*/
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

    public Task<CivitEnumsResponse> GetEnums()
    {
        return civitApi.GetEnums();
    }

    public Task<HttpResponseMessage> GetBaseModelList()
    {
        return civitApi.GetBaseModelList();
    }
}
