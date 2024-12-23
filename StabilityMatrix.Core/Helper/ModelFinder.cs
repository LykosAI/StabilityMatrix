using System.Net;
using Injectio.Attributes;
using NLog;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Helper;

// return Model, ModelVersion, ModelFile
public record struct ModelSearchResult(CivitModel Model, CivitModelVersion ModelVersion, CivitFile ModelFile);

[RegisterSingleton<ModelFinder>]
public class ModelFinder
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ILiteDbContext liteDbContext;
    private readonly ICivitApi civitApi;

    public ModelFinder(ILiteDbContext liteDbContext, ICivitApi civitApi)
    {
        this.liteDbContext = liteDbContext;
        this.civitApi = civitApi;
    }

    // Finds a model from the local database using file hash
    public async Task<ModelSearchResult?> LocalFindModel(string hashBlake3)
    {
        var (model, version) = await liteDbContext.FindCivitModelFromFileHashAsync(hashBlake3);
        if (model == null || version == null)
        {
            return null;
        }

        var file = version.Files!.First(file => file.Hashes.BLAKE3?.ToLowerInvariant() == hashBlake3);

        return new ModelSearchResult(model, version, file);
    }

    // Finds a model using Civit API using file hash
    public async Task<ModelSearchResult?> RemoteFindModel(string hashBlake3)
    {
        Logger.Info("Searching Civit API for model version using hash {Hash}", hashBlake3);
        try
        {
            var versionResponse = await civitApi.GetModelVersionByHash(hashBlake3);

            Logger.Info(
                "Found version {VersionId} with model id {ModelId}",
                versionResponse.Id,
                versionResponse.ModelId
            );
            var model = await civitApi.GetModelById(versionResponse.ModelId);

            // VersionResponse is not actually the full data of ModelVersion, so find it again
            var version = model.ModelVersions!.First(version => version.Id == versionResponse.Id);

            var file = versionResponse.Files.FirstOrDefault(
                file => hashBlake3.Equals(file.Hashes.BLAKE3, StringComparison.OrdinalIgnoreCase)
            );

            // Archived models do not have files
            if (file == null)
                return null;

            return new ModelSearchResult(model, version, file);
        }
        catch (TaskCanceledException e)
        {
            Logger.Warn(
                "Timed out while finding remote model version using hash {Hash}: {Error}",
                hashBlake3,
                e.Message
            );
            return null;
        }
        catch (ApiException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Info("Could not find remote model version using hash {Hash}", hashBlake3);
            }
            else
            {
                Logger.Warn(
                    e,
                    "Could not find remote model version using hash {Hash}: {Error}",
                    hashBlake3,
                    e.Message
                );
            }

            return null;
        }
        catch (HttpRequestException e)
        {
            Logger.Warn(
                e,
                "Could not connect to api while finding remote model version using hash {Hash}: {Error}",
                hashBlake3,
                e.Message
            );
            return null;
        }
    }

    public async Task<IEnumerable<CivitModel>> FindRemoteModelsById(IEnumerable<int> ids)
    {
        var results = new List<CivitModel>();

        // split ids into batches of 100
        var batches = ids.Chunk(100);

        foreach (var batch in batches)
        {
            try
            {
                var response = await civitApi
                    .GetModels(
                        new CivitModelsRequest
                        {
                            CommaSeparatedModelIds = string.Join(",", batch),
                            Nsfw = "true",
                            Query = string.Empty
                        }
                    )
                    .ConfigureAwait(false);

                if (response.Items == null || response.Items.Count == 0)
                    continue;

                results.AddRange(response.Items);
            }
            catch (Exception e)
            {
                Logger.Error("Error while finding remote models by id: {Error}", e.Message);
            }
        }

        return results;
    }
}
