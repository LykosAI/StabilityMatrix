using System.Diagnostics.CodeAnalysis;
using Apizr;
using Apizr.Caching;
using Apizr.Caching.Attributes;
using Apizr.Configuring.Manager;
using Apizr.Connecting;
using Apizr.Mapping;
using Fusillade;
using Polly.Registry;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Core.Services;

public class OpenModelDbManager(
    ILazyFactory<IOpenModelDbApi> lazyWebApi,
    IConnectivityHandler connectivityHandler,
    ICacheHandler cacheHandler,
    IMappingHandler mappingHandler,
    ILazyFactory<ResiliencePipelineRegistry<string>> lazyResiliencePipelineRegistry,
    IApizrManagerOptions<IOpenModelDbApi> apizrOptions
)
    : ApizrManager<IOpenModelDbApi>(
        lazyWebApi,
        connectivityHandler,
        cacheHandler,
        mappingHandler,
        lazyResiliencePipelineRegistry,
        apizrOptions
    )
{
    public Uri UsersBaseUri => new("https://openmodeldb.info/users");

    public Uri ModelsBaseUri => new("https://openmodeldb.info/models");

    public IReadOnlyDictionary<string, OpenModelDbTag>? Tags { get; private set; }

    public IReadOnlyDictionary<string, OpenModelDbArchitecture>? Architectures { get; private set; }

    [MemberNotNull(nameof(Tags), nameof(Architectures))]
    public async Task EnsureMetadataLoadedAsync(Priority priority = default)
    {
        if (Tags is null)
        {
            Tags = await ExecuteAsync(api => api.GetTags()).ConfigureAwait(false);
        }
        if (Architectures is null)
        {
            Architectures = await ExecuteAsync(api => api.GetArchitectures()).ConfigureAwait(false);
        }
    }
}
