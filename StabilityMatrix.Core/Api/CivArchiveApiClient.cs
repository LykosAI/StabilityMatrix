using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Models.Api.CivArchive;

namespace StabilityMatrix.Core.Api;

[RegisterSingleton<ICivArchiveApiClient, CivArchiveApiClient>]
public partial class CivArchiveApiClient(
    ILogger<CivArchiveApiClient> logger,
    IHttpClientFactory httpClientFactory
) : ICivArchiveApiClient
{
    private static readonly Uri BaseUri = new("https://civarchive.com");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient httpClient = CreateHttpClient(httpClientFactory);
    private readonly SemaphoreSlim buildIdLock = new(1, 1);
    private string? cachedBuildId;

    public async Task<string> GetBuildIdAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(cachedBuildId))
        {
            return cachedBuildId;
        }

        await buildIdLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(cachedBuildId))
            {
                return cachedBuildId;
            }

            cachedBuildId = await ResolveBuildIdAsync(cancellationToken);
            return cachedBuildId;
        }
        finally
        {
            buildIdLock.Release();
        }
    }

    public async Task<CivArchiveSearchResponse> SearchAsync(
        CivArchiveSearchFilters filters,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(filters);

        var routePath = string.IsNullOrWhiteSpace(filters.RoutePath) ? "/top-models" : filters.RoutePath;
        var relativePath = BuildSearchDataPath(routePath, filters);
        var response = await GetNextDataAsync<CivArchiveListPageResponse>(relativePath, cancellationToken);

        var pageProps =
            response.PageProps
            ?? throw new InvalidOperationException("CivArchive list page was missing pageProps");

        var effectiveFilters = pageProps.Filters?.ToSearchFilters() ?? filters;

        return new CivArchiveSearchResponse
        {
            Results = pageProps.Data?.Results ?? [],
            FilterOptions = new CivArchiveFilterOptions
            {
                BaseModels = pageProps.FilterOptions?.BaseModels ?? [],
                ModelTypes = pageProps.FilterOptions?.ModelTypes ?? [],
            },
            EffectiveFilters = effectiveFilters,
            CanonicalUrl = pageProps.CanonicalUrl ?? string.Empty,
            Hits = pageProps.Data?.Hits ?? 0,
            TotalHits = pageProps.Data?.TotalHits ?? 0,
        };
    }

    public async Task<CivArchiveModelDetailsResponse> GetModelDetailsAsync(
        string relativeUrl,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            throw new ArgumentException("Relative URL is required", nameof(relativeUrl));
        }

        var nextDataPath = BuildDetailDataPath(relativeUrl);
        var response = await GetNextDataAsync<CivArchiveDetailPageResponse>(nextDataPath, cancellationToken);

        var pageProps =
            response.PageProps
            ?? throw new InvalidOperationException("CivArchive detail page was missing pageProps");

        var model =
            pageProps.Model
            ?? throw new InvalidOperationException("CivArchive detail page was missing model data");

        // The API returns version, platform, and platform_name as siblings of model
        // under pageProps, so merge them into the model object
        if (pageProps.Version is not null)
        {
            model.Version = pageProps.Version;
        }

        model.Platform ??= pageProps.Platform;
        model.PlatformName ??= pageProps.PlatformName;

        return new CivArchiveModelDetailsResponse { Model = model };
    }

    public Uri GetAbsoluteUri(string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return BaseUri;
        }

        return Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(BaseUri, relativeUrl);
    }

    public static string BuildSearchDataPath(string routePath, CivArchiveSearchFilters filters)
    {
        var query = new List<string>
        {
            $"platform={Uri.EscapeDataString(filters.Platform.ToApiString())}",
            $"sort={Uri.EscapeDataString(filters.Sort.ToApiString())}",
            $"rating={Uri.EscapeDataString(filters.Rating.ToApiString())}",
            $"platform_status={Uri.EscapeDataString(filters.PlatformStatus.ToApiString())}",
            $"kind={Uri.EscapeDataString(filters.Kind.ToApiString())}",
            $"period={Uri.EscapeDataString(filters.Period.ToApiString())}",
            $"page={filters.Page}",
        };

        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            query.Add($"q={Uri.EscapeDataString(filters.Query)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Tags))
        {
            query.Add($"tags={Uri.EscapeDataString(filters.Tags)}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Username))
        {
            query.Add($"username={Uri.EscapeDataString(filters.Username)}");
        }

        if (filters.Types.Count > 0)
        {
            query.Add($"type={Uri.EscapeDataString(string.Join(",", filters.Types))}");
        }

        if (filters.BaseModels.Count > 0)
        {
            query.Add($"base_model={Uri.EscapeDataString(string.Join(",", filters.BaseModels))}");
        }

        return $"{NormalizeRoutePath(routePath)}.json?{string.Join("&", query)}";
    }

    public static string BuildDetailDataPath(string relativeUrl)
    {
        var uri = new Uri(BaseUri, relativeUrl);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Relative URL did not contain a route path");
        }

        // Apply Next.js rewrite rules: /{platform}/models/{id}/versions/{versionId}
        // rewrites to /models/{id}?modelVersionId={versionId}&platform={platform}
        var rewriteMatch = PlatformDetailRewriteRegex().Match(path);
        if (rewriteMatch.Success)
        {
            var platform = rewriteMatch.Groups["platform"].Value;
            var modelId = rewriteMatch.Groups["modelId"].Value;
            var versionId = rewriteMatch.Groups["versionId"].Value;

            return $"/models/{modelId}.json?modelVersionId={versionId}&platform={platform}";
        }

        // Also handle /{platform}/models/{id} (without version)
        var platformModelMatch = PlatformModelRewriteRegex().Match(path);
        if (platformModelMatch.Success)
        {
            var platform = platformModelMatch.Groups["platform"].Value;
            var modelId = platformModelMatch.Groups["modelId"].Value;

            return $"/models/{modelId}.json?platform={platform}";
        }

        // Handle /models/{id}/{slug} → /models/{id}
        var slugMatch = ModelSlugRewriteRegex().Match(path);
        if (slugMatch.Success)
        {
            var modelId = slugMatch.Groups["modelId"].Value;

            return $"/models/{modelId}.json{uri.Query}";
        }

        return $"{path}.json{uri.Query}";
    }

    private async Task<TResponse> GetNextDataAsync<TResponse>(
        string routeWithQuery,
        CancellationToken cancellationToken
    )
    {
        return await GetNextDataAsync<TResponse>(
            routeWithQuery,
            allowBuildIdRefresh: true,
            cancellationToken
        );
    }

    private async Task<TResponse> GetNextDataAsync<TResponse>(
        string routeWithQuery,
        bool allowBuildIdRefresh,
        CancellationToken cancellationToken
    )
    {
        var buildId = await GetBuildIdAsync(cancellationToken);
        var url = $"/_next/data/{buildId}{routeWithQuery}";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound && allowBuildIdRefresh)
        {
            logger.LogInformation("CivArchive buildId {BuildId} appears stale, refreshing", buildId);
            await RefreshBuildIdAsync(cancellationToken);
            return await GetNextDataAsync<TResponse>(
                routeWithQuery,
                allowBuildIdRefresh: false,
                cancellationToken
            );
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return (await JsonSerializer.DeserializeAsync<TResponse>(stream, JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Failed to deserialize CivArchive response");
    }

    private async Task RefreshBuildIdAsync(CancellationToken cancellationToken)
    {
        await buildIdLock.WaitAsync(cancellationToken);
        try
        {
            cachedBuildId = await ResolveBuildIdAsync(cancellationToken);
        }
        finally
        {
            buildIdLock.Release();
        }
    }

    private async Task<string> ResolveBuildIdAsync(CancellationToken cancellationToken)
    {
        var html = await httpClient.GetStringAsync("/", cancellationToken);
        var match = BuildIdRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Unable to resolve CivArchive buildId");
        }

        return match.Groups["buildId"].Value;
    }

    private static HttpClient CreateHttpClient(IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress ??= BaseUri;
        client.Timeout = TimeSpan.FromSeconds(60);
        return client;
    }

    private static string NormalizeRoutePath(string routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath))
        {
            return "/top-models";
        }

        return routePath.StartsWith('/') ? routePath : "/" + routePath;
    }

    [GeneratedRegex("\"buildId\":\"(?<buildId>[^\"]+)\"")]
    private static partial Regex BuildIdRegex();

    private const string PlatformNames =
        "tensorart|seaart|civision|pixai|tungsten|yodayo|moescape|shakker|tensorhub|civitai|huggingface|modelscope";

    // Matches /{platform}/models/{id}/versions/{versionId}
    [GeneratedRegex(
        $@"^/(?<platform>{PlatformNames})/models/(?<modelId>[^/]+)/versions/(?<versionId>[^/]+)$"
    )]
    private static partial Regex PlatformDetailRewriteRegex();

    // Matches /{platform}/models/{id} (without version)
    [GeneratedRegex($@"^/(?<platform>{PlatformNames})/models/(?<modelId>[^/]+)$")]
    private static partial Regex PlatformModelRewriteRegex();

    // Matches /models/{id}/{slug} (slug rewrite to /models/{id})
    [GeneratedRegex(@"^/models/(?<modelId>[^/]+)/(?<slug>[^/?]+)$")]
    private static partial Regex ModelSlugRewriteRegex();

    private sealed class CivArchiveListPageResponse
    {
        [JsonPropertyName("pageProps")]
        public CivArchiveListPageProps? PageProps { get; set; }
    }

    private sealed class CivArchiveListPageProps
    {
        [JsonPropertyName("canonicalUrl")]
        public string? CanonicalUrl { get; set; }

        [JsonPropertyName("data")]
        public CivArchiveListData? Data { get; set; }

        [JsonPropertyName("filterOptions")]
        public CivArchiveFilterOptionsDto? FilterOptions { get; set; }

        [JsonPropertyName("filters")]
        public CivArchiveFiltersDto? Filters { get; set; }
    }

    private sealed class CivArchiveListData
    {
        [JsonPropertyName("results")]
        public List<CivArchiveSearchResult> Results { get; set; } = [];

        [JsonPropertyName("hits")]
        public int Hits { get; set; }

        [JsonPropertyName("totalHits")]
        public int TotalHits { get; set; }
    }

    private sealed class CivArchiveFilterOptionsDto
    {
        [JsonPropertyName("baseModels")]
        public List<string> BaseModels { get; set; } = [];

        [JsonPropertyName("modelTypes")]
        public List<string> ModelTypes { get; set; } = [];
    }

    private sealed class CivArchiveDetailPageResponse
    {
        [JsonPropertyName("pageProps")]
        public CivArchiveDetailPageProps? PageProps { get; set; }
    }

    private sealed class CivArchiveDetailPageProps
    {
        [JsonPropertyName("model")]
        public CivArchiveModelDetails? Model { get; set; }

        [JsonPropertyName("version")]
        public CivArchiveModelVersion? Version { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("platform_name")]
        public string? PlatformName { get; set; }
    }

    private sealed class CivArchiveFiltersDto
    {
        [JsonPropertyName("q")]
        public string? Query { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(CivArchiveStringOrArrayConverter))]
        public List<string> Types { get; set; } = [];

        [JsonPropertyName("base_model")]
        [JsonConverter(typeof(CivArchiveStringOrArrayConverter))]
        public List<string> BaseModels { get; set; } = [];

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("sort")]
        public string? Sort { get; set; }

        [JsonPropertyName("rating")]
        public string? Rating { get; set; }

        [JsonPropertyName("platform_status")]
        public string? PlatformStatus { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("period")]
        public string? Period { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        public CivArchiveSearchFilters ToSearchFilters()
        {
            return new CivArchiveSearchFilters
            {
                Query = Query ?? string.Empty,
                Types = Types is ["all"] ? [] : Types,
                BaseModels = BaseModels is ["all"] ? [] : BaseModels,
                Platform = CivArchiveEnumExtensions.ParsePlatform(Platform ?? "all"),
                Sort = CivArchiveEnumExtensions.ParseSort(Sort ?? "top"),
                Rating = CivArchiveEnumExtensions.ParseRating(Rating ?? "safe"),
                PlatformStatus = CivArchiveEnumExtensions.ParsePlatformStatus(PlatformStatus ?? "all"),
                Kind = CivArchiveEnumExtensions.ParseKind(Kind ?? "all"),
                Tags = Tags ?? string.Empty,
                Username = Username ?? string.Empty,
                Period = CivArchiveEnumExtensions.ParsePeriod(Period ?? "all"),
                Page = Page,
            };
        }
    }

    private sealed class CivArchiveStringOrArrayConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            var results = new List<string>();

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value);
                }

                return results;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Unsupported token {reader.TokenType} for string array");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return results;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        results.Add(value);
                    }
                }
            }

            return results;
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }
    }
}
