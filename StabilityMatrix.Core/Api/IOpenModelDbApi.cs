using System.Text.Json.Serialization;
using Apizr.Caching;
using Apizr.Caching.Attributes;
using Apizr.Configuring;
using Refit;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Core.Api;

[BaseAddress("https://openmodeldb.info")]
public interface IOpenModelDbApi
{
    [Get("/api/v1/models.json"), Cache(CacheMode.GetOrFetch, "0.00:02:00")]
    Task<OpenModelDbModelsResponse> GetModels();

    [Get("/api/v1/tags.json"), Cache(CacheMode.GetOrFetch, "0.00:10:00")]
    Task<OpenModelDbTagsResponse> GetTags();

    [Get("/api/v1/architectures.json"), Cache(CacheMode.GetOrFetch, "0.00:10:00")]
    Task<OpenModelDbArchitecturesResponse> GetArchitectures();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OpenModelDbModelsResponse))]
[JsonSerializable(typeof(OpenModelDbTagsResponse))]
[JsonSerializable(typeof(OpenModelDbArchitecturesResponse))]
public partial class OpenModelDbApiJsonContext : JsonSerializerContext;
