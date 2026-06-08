using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.CivArchive;

namespace StabilityMatrix.UITests.Fakes;

public class TestCivArchiveApiClient : ICivArchiveApiClient
{
    public Task<string> GetBuildIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("test-build");
    }

    public Task<CivArchiveSearchResponse> SearchAsync(
        CivArchiveSearchFilters filters,
        CancellationToken cancellationToken = default
    )
    {
        var page = filters.Page;
        IReadOnlyList<CivArchiveSearchResult> results = page switch
        {
            1 => new List<CivArchiveSearchResult>
            {
                new CivArchiveSearchResult
                {
                    Id = "v-1",
                    Name = "Test Version 1",
                    KindRaw = "version",
                    Type = "Checkpoint",
                    BaseModel = "Pony",
                    Url = "/models/1?modelVersionId=11",
                    Username = "artist-one",
                    Platform = "civitai",
                },
                new CivArchiveSearchResult
                {
                    Id = "f-1",
                    Name = "test-file-1.safetensors",
                    KindRaw = "file",
                    Url = "/sha256/abc123",
                    Username = "artist-one",
                    Platform = "huggingface",
                },
            },
            2 => new List<CivArchiveSearchResult>
            {
                new CivArchiveSearchResult
                {
                    Id = "v-2",
                    Name = "Test Version 2",
                    KindRaw = "version",
                    Type = "LORA",
                    BaseModel = "Illustrious",
                    Url = "/models/2?modelVersionId=22",
                    Username = "artist-two",
                    Platform = "seaart",
                },
                new CivArchiveSearchResult
                {
                    Id = "u-1",
                    Name = "artist-two",
                    KindRaw = "user",
                    Url = "/users/artist-two",
                    Username = "artist-two",
                    Platform = "seaart",
                },
            },
            _ => [],
        };

        return Task.FromResult(
            new CivArchiveSearchResponse
            {
                Results = results,
                FilterOptions = new CivArchiveFilterOptions
                {
                    BaseModels = ["Illustrious", "Pony"],
                    ModelTypes = ["LORA", "Checkpoint"],
                },
                EffectiveFilters = new CivArchiveSearchFilters
                {
                    Page = page,
                    Platform = filters.Platform,
                    Sort = filters.Sort,
                    Period = filters.Period,
                    Rating = filters.Rating,
                    PlatformStatus = filters.PlatformStatus,
                    Kind = filters.Kind,
                    Query = filters.Query,
                    Tags = filters.Tags,
                    Username = filters.Username,
                },
                CanonicalUrl = "https://civarchive.com/top-models",
                Hits = results.Count,
                TotalHits = 4,
            }
        );
    }

    public Task<CivArchiveFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new CivArchiveFilterOptions
            {
                BaseModels = ["Illustrious", "Pony"],
                ModelTypes = ["LORA", "Checkpoint"],
            }
        );
    }

    public Task<CivArchiveModelDetailsResponse> GetModelDetailsAsync(
        string relativeUrl,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(
            new CivArchiveModelDetailsResponse
            {
                Model = new CivArchiveModelDetails
                {
                    Name = "Test Model",
                    PlatformName = "CivitAI",
                    Type = "Checkpoint",
                    Version = new CivArchiveModelVersion
                    {
                        Name = "Test Version",
                        BaseModel = "Pony",
                        Files =
                        [
                            new CivArchiveModelFile
                            {
                                Name = "test.safetensors",
                                Sha256 = "abc123",
                                Mirrors =
                                [
                                    new CivArchiveFileMirror
                                    {
                                        Source = "civitai",
                                        Url = "https://example.org/file",
                                    },
                                ],
                            },
                        ],
                    },
                },
            }
        );
    }

    public Task<string?> ResolveFileUrlAsync(
        string sha256RelativeUrl,
        CancellationToken cancellationToken = default
    )
    {
        // Map our seeded test file to the matching test version. Anything else returns null
        // so the caller falls back to opening the URL externally.
        return Task.FromResult<string?>(
            sha256RelativeUrl == "/sha256/abc123" ? "/models/1?modelVersionId=11" : null
        );
    }

    public Uri GetAbsoluteUri(string relativeUrl) => new($"https://civarchive.com{relativeUrl}");
}
