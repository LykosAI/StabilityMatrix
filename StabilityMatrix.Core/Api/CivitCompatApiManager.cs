using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Refit;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
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
    ICivitTRPCApi civitTrpcApi,
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
        return GetModelByIdInternal(id);
    }

    private async Task<CivitModel> GetModelByIdInternal(int id)
    {
        // Note: Discovery API path intentionally not used here — it's subscriber-only and
        // doesn't help free users hit by the public REST cache-desync issue (where
        // modelVersions comes back empty for models with newly-added/updated versions).
        // The tRPC fallback below handles that case for everyone.
        var model = await civitApi.GetModelById(id).ConfigureAwait(false);

        if (model is { ModelVersions: null or { Count: 0 } })
        {
            await TryFillModelVersionsFromTrpc(model).ConfigureAwait(false);
        }

        return model;
    }

    /// <summary>
    /// Best-effort fallback: when the public REST API returns a model with an empty
    /// <c>modelVersions</c> list (a known CivitAI server-side cache-desync bug), try the
    /// internal tRPC <c>model.getById</c> endpoint — the same one the website uses — to
    /// recover the versions+files data. Any failure here is swallowed and logged so we
    /// preserve the original REST response rather than crashing the caller.
    /// </summary>
    private async Task TryFillModelVersionsFromTrpc(CivitModel model)
    {
        try
        {
            logger.LogInformation(
                "REST API returned empty modelVersions for model {Id}; attempting tRPC fallback",
                model.Id
            );

            var trpcResponse = await civitTrpcApi.GetModelById(model.Id).ConfigureAwait(false);
            var trpcModel = trpcResponse.Result.Data.Json;
            var versions = CivitTRPCMapper.ToModelVersions(trpcModel);

            if (versions.Count == 0)
            {
                logger.LogInformation("tRPC fallback for model {Id} also returned no versions", model.Id);
                return;
            }

            model.ModelVersions = versions;
            logger.LogInformation(
                "tRPC fallback recovered {Count} version(s) for model {Id}",
                versions.Count,
                model.Id
            );
        }
        catch (ApiException ex)
        {
            // 401 is the loud "stop using tRPC" signal CivitAI returns when they detect
            // non-website usage. Worth surfacing if it ever starts happening at scale.
            logger.LogWarning(
                ex,
                "tRPC fallback for model {Id} failed with {StatusCode}; returning empty modelVersions",
                model.Id,
                ex.StatusCode
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "tRPC fallback for model {Id} threw; returning empty modelVersions",
                model.Id
            );
        }
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
