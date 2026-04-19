using System.Text.Json.Nodes;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<ICivitBaseModelTypeService, CivitBaseModelTypeService>]
public class CivitBaseModelTypeService(
    ILogger<CivitBaseModelTypeService> logger,
    ICivitApi civitApi,
    ILiteDbContext dbContext
) : ICivitBaseModelTypeService
{
    private const string CacheId = "BaseModelTypes";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private const string LegacyBaseModelListProbe = "gimmethelist";
    private static readonly IReadOnlyList<string> KnownVisibleBaseModelTypes =
    [
        "Anima",
        "AuraFlow",
        "Chroma",
        "CogVideoX",
        "Flux.1 D",
        "Flux.1 Kontext",
        "Flux.1 Krea",
        "Flux.1 S",
        "Flux.2 D",
        "Flux.2 Klein 4B",
        "Flux.2 Klein 4B-base",
        "Flux.2 Klein 9B",
        "Flux.2 Klein 9B-base",
        "Grok",
        "HiDream",
        "Hunyuan 1",
        "Hunyuan Video",
        "Illustrious",
        "Kolors",
        "LTXV",
        "LTXV 2.3",
        "LTXV2",
        "Lumina",
        "Mochi",
        "NoobAI",
        "Other",
        "PixArt E",
        "PixArt a",
        "Pony",
        "Pony V7",
        "Qwen",
        "Qwen 2",
        "SD 1.4",
        "SD 1.5",
        "SD 1.5 Hyper",
        "SD 1.5 LCM",
        "SD 2.0",
        "SD 2.1",
        "SDXL 1.0",
        "SDXL Hyper",
        "SDXL Lightning",
        "Upscaler",
        "Wan Image 2.7",
        "Wan Video 1.3B t2v",
        "Wan Video 14B i2v 480p",
        "Wan Video 14B i2v 720p",
        "Wan Video 14B t2v",
        "Wan Video 2.2 I2V-A14B",
        "Wan Video 2.2 T2V-A14B",
        "Wan Video 2.2 TI2V-5B",
        "Wan Video 2.5 I2V",
        "Wan Video 2.5 T2V",
        "Wan Video 2.7",
        "ZImageBase",
        "ZImageTurbo",
    ];

    /// <summary>
    /// Gets the list of base model types, using cache if available and not expired
    /// </summary>
    public async Task<List<string>> GetBaseModelTypes(bool forceRefresh = false, bool includeAllOption = true)
    {
        List<string> civitBaseModels = [];

        if (!forceRefresh)
        {
            var cached = await dbContext.GetCivitBaseModelTypeCacheEntry(CacheId);
            if (cached != null && DateTimeOffset.UtcNow.Subtract(cached.CreatedAt) < CacheExpiration)
            {
                civitBaseModels = cached.ModelTypes;
            }
        }

        try
        {
            if (civitBaseModels.Count <= 0)
            {
                civitBaseModels =
                    await TryGetBaseModelsFromEnumsEndpoint() ?? await TryGetLegacyBaseModelList();

                civitBaseModels =
                    civitBaseModels.Count > 0 ? civitBaseModels : GetKnownVisibleBaseModelTypes();

                // Cache the results
                var cacheEntry = new CivitBaseModelTypeCacheEntry
                {
                    Id = CacheId,
                    ModelTypes = civitBaseModels,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                await dbContext.UpsertCivitBaseModelTypeCacheEntry(cacheEntry);
            }

            return NormalizeBaseModelTypes(civitBaseModels, includeAllOption);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get base model list");

            // Return cached results if available, even if expired
            var expiredCache = await dbContext.GetCivitBaseModelTypeCacheEntry(CacheId);
            return NormalizeBaseModelTypes(
                expiredCache?.ModelTypes ?? GetKnownVisibleBaseModelTypes(),
                includeAllOption
            );
        }
    }

    /// <summary>
    /// Clears the cached base model types
    /// </summary>
    public void ClearCache()
    {
        dbContext.CivitBaseModelTypeCache.DeleteAllAsync();
    }

    private async Task<List<string>?> TryGetBaseModelsFromEnumsEndpoint()
    {
        try
        {
            var enumsResponse = await civitApi.GetEnums();
            var baseModels = enumsResponse.ActiveBaseModel ?? enumsResponse.BaseModel;

            if (baseModels is { Count: > 0 })
            {
                return baseModels;
            }

            logger.LogInformation(
                "CivitAI enums endpoint returned no base models; falling back to legacy/base list"
            );
            return null;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "CivitAI enums endpoint failed; falling back to legacy/base list");
            return null;
        }
    }

    private async Task<List<string>?> TryGetLegacyBaseModelList()
    {
        var baseModelsResponse = await civitApi.GetBaseModelList();
        var jsonContent = await baseModelsResponse.Content.ReadAsStringAsync();
        return TryParseLegacyBaseModelList(jsonContent);
    }

    private List<string>? TryParseLegacyBaseModelList(string jsonContent)
    {
        var baseModels = JsonNode.Parse(jsonContent);
        var innerJson = baseModels?["error"]?["message"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(innerJson))
        {
            logger.LogInformation(
                "CivitAI base model probe value '{Probe}' no longer returns the legacy validation payload; using built-in base model list",
                LegacyBaseModelListProbe
            );
            return null;
        }

        var jArray = JsonNode.Parse(innerJson)?.AsArray();
        var baseModelValues = jArray?[0]?["errors"]?[0]?[0]?["values"]?.AsArray();
        return baseModelValues?.GetValues<string>().ToList();
    }

    private static List<string> GetKnownVisibleBaseModelTypes()
    {
        return KnownVisibleBaseModelTypes.ToList();
    }

    private static List<string> NormalizeBaseModelTypes(
        IEnumerable<string>? baseModels,
        bool includeAllOption
    )
    {
        var normalized = (baseModels ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => !s.Equals("odor", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        normalized.RemoveAll(s =>
            s.Equals(CivitBaseModelType.All.ToString(), StringComparison.OrdinalIgnoreCase)
        );

        if (includeAllOption)
        {
            normalized.Insert(0, CivitBaseModelType.All.ToString());
        }

        return normalized;
    }
}
