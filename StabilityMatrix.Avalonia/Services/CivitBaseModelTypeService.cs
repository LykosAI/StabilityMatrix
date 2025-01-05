using System.Text.Json.Nodes;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
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
                var baseModelsResponse = await civitApi.GetBaseModelList();
                var jsonContent = await baseModelsResponse.Content.ReadAsStringAsync();
                var baseModels = JsonNode.Parse(jsonContent);

                var jArray =
                    baseModels?["error"]?["issues"]?[0]?["unionErrors"]?[0]?["issues"]?[0]?["options"]
                    as JsonArray;

                civitBaseModels = jArray?.GetValues<string>().ToList() ?? [];

                // Cache the results
                var cacheEntry = new CivitBaseModelTypeCacheEntry
                {
                    Id = CacheId,
                    ModelTypes = civitBaseModels,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await dbContext.UpsertCivitBaseModelTypeCacheEntry(cacheEntry);
            }

            if (includeAllOption)
            {
                civitBaseModels.Insert(0, CivitBaseModelType.All.ToString());
            }

            // Filter and sort results
            var filteredResults = civitBaseModels
                .Where(s => !s.Equals("odor", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();

            return filteredResults;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get base model list");

            // Return cached results if available, even if expired
            var expiredCache = await dbContext.GetCivitBaseModelTypeCacheEntry(CacheId);
            return expiredCache?.ModelTypes ?? [];
        }
    }

    /// <summary>
    /// Clears the cached base model types
    /// </summary>
    public void ClearCache()
    {
        dbContext.CivitBaseModelTypeCache.DeleteAllAsync();
    }
}
