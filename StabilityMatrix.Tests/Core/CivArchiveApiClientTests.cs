using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.CivArchive;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivArchiveApiClientTests
{
    [TestMethod]
    public void BuildSearchDataPath_UsesDefaultFilterValues()
    {
        var result = CivArchiveApiClient.BuildSearchDataPath("/top-models", new CivArchiveSearchFilters());

        Assert.AreEqual(
            "/top-models.json?platform=all&sort=top&rating=safe&platform_status=all&kind=all&period=all&page=1",
            result
        );
    }

    [TestMethod]
    public void BuildSearchDataPath_SerializesMultiSelectFilters()
    {
        var result = CivArchiveApiClient.BuildSearchDataPath(
            "/top-models",
            new CivArchiveSearchFilters
            {
                Types = ["LORA", "Checkpoint"],
                BaseModels = ["Illustrious", "Pony"],
                Page = 2,
            }
        );

        StringAssert.Contains(result, "type=LORA%2CCheckpoint");
        StringAssert.Contains(result, "base_model=Illustrious%2CPony");
        StringAssert.Contains(result, "page=2");
    }

    [TestMethod]
    public void BuildDetailDataPath_RewritesModelScopeCnPlatformRoutes()
    {
        var result = CivArchiveApiClient.BuildDetailDataPath("/modelscope_cn/models/123/versions/456");

        Assert.AreEqual("/models/123.json?modelVersionId=456&platform=modelscope_cn", result);
    }

    [TestMethod]
    public async Task SearchAsync_RefreshesBuildIdAfter404()
    {
        var requests = new List<string>();
        var responses = new Queue<HttpResponseMessage>(
            [
                CreateJsonResponse("""<html><script>{"buildId":"old-build"}</script></html>""", "text/html"),
                new HttpResponseMessage(HttpStatusCode.NotFound),
                CreateJsonResponse("""<html><script>{"buildId":"new-build"}</script></html>""", "text/html"),
                CreateJsonResponse(
                    ListResponseJson(
                        """{"id":"v1","name":"Model","kind":"version","url":"/models/1?modelVersionId=2"}"""
                    )
                ),
            ]
        );

        var client = CreateClient(
            new RecordingHandler(
                (request, _) =>
                {
                    requests.Add(request.RequestUri!.ToString());
                    return responses.Dequeue();
                }
            )
        );

        var response = await client.SearchAsync(new CivArchiveSearchFilters());

        Assert.AreEqual(1, response.Results.Count);
        Assert.IsTrue(requests.Any(x => x.Contains("/_next/data/old-build/")));
        Assert.IsTrue(requests.Any(x => x.Contains("/_next/data/new-build/")));
    }

    [TestMethod]
    public async Task SearchAsync_ParsesVersionAndFileResults()
    {
        var listJson = ListResponseJson(
            """
            {"id":"v2581228","name":"CyberRealistic Pony v16.0","type":"Checkpoint","kind":"version","download_count":59326,"url":"/models/443821?modelVersionId=2581228","base_model":"Pony","image_url":"https://example.org/image.jpg","created_at":1767967595,"username":"Cyberdelia","platform":"civitai"},
            {"id":"rf_hash","name":"realDream_14Hyper.safetensors","kind":"file","download_count":0,"url":"/sha256/a00019e86d53aece9858347e4df8a774a6d2933c30d0691faa9beb0cc56e7366","username":"Carlos2312","platform":"huggingface","created_at":1771938405}
            """
        );

        var responses = new Queue<HttpResponseMessage>(
            [
                CreateJsonResponse("""<html><script>{"buildId":"test-build"}</script></html>""", "text/html"),
                CreateJsonResponse(listJson),
            ]
        );

        var client = CreateClient(new RecordingHandler((_, _) => responses.Dequeue()));
        var response = await client.SearchAsync(new CivArchiveSearchFilters());

        Assert.AreEqual(2, response.Results.Count);
        Assert.AreEqual(CivArchiveKindOption.Version, response.Results[0].Kind);
        Assert.AreEqual(CivArchiveKindOption.File, response.Results[1].Kind);
        Assert.AreEqual(
            "a00019e86d53aece9858347e4df8a774a6d2933c30d0691faa9beb0cc56e7366",
            response.Results[1].Sha256FromUrl
        );
    }

    [TestMethod]
    public async Task GetModelDetailsAsync_ParsesFilesMirrorsAndSha256()
    {
        // Field naming matches the real CivArchive API (snake_case throughout)
        const string detailJson = """
            {"pageProps":{"model":{"id":153568,"name":"Real Dream","type":"Checkpoint","download_count":4923593,"favorite_count":12,"rating":4.5,"rating_count":8,"created_at":"2026-04-17T23:30:06Z","creator_username":"sinatra","platform":"civitai","platform_name":"CivitAI","version":{"id":2053273,"name":"SDXL 7","base_model":"SDXL 1.0","description":"<p>Version description</p>","download_count":12345,"created_at":"2026-04-17T23:30:06Z","files":[{"id":1950275,"name":"realDream_sdxl7.safetensors","type":"Model","size_kb":6775783.6,"download_url":"https://civitai.com/api/download/models/2053273","sha256":"63b1db60611f52c4fbb2cade67dbdf4029c6620c5b22f2a4ddb27a47d7601953","is_primary":true,"created_at":"2026-04-17T23:30:06Z","mirrors":[{"filename":"realDream_sdxl7.safetensors","url":"https://civitai.com/api/download/models/2053273","source":"civitai","is_gated":false,"is_paid":false}]}],"images":[{"id":1,"url":"https://example.org/image.webp","link":"https://example.org/image.webp","type":"image"}],"mirrors":[{"platform":"tungsten","platform_url":"https://tungsten.run/model/kZ7yDBQjZP?model_version=L2KrgferKS","version_name":"SDXL 7"}]}}}}
            """;

        var responses = new Queue<HttpResponseMessage>(
            [
                CreateJsonResponse("""<html><script>{"buildId":"test-build"}</script></html>""", "text/html"),
                CreateJsonResponse(detailJson),
            ]
        );

        var client = CreateClient(new RecordingHandler((_, _) => responses.Dequeue()));
        var response = await client.GetModelDetailsAsync("/models/153568?modelVersionId=2053273");

        Assert.AreEqual("Real Dream", response.Model.Name);
        Assert.AreEqual("SDXL 7", response.Model.Version?.Name);
        Assert.AreEqual(1, response.Model.Version?.Files.Count);
        Assert.AreEqual(
            "63b1db60611f52c4fbb2cade67dbdf4029c6620c5b22f2a4ddb27a47d7601953",
            response.Model.Version?.Files[0].Sha256
        );
        Assert.AreEqual(1, response.Model.Version?.Mirrors.Count);

        // Snake-case field names — these all silently defaulted to 0/null when the DTO
        // mapped them to camelCase (downloadCount/baseModel/sizeKB/etc.).
        Assert.AreEqual(4923593, response.Model.DownloadCount);
        Assert.AreEqual(12, response.Model.FavoriteCount);
        Assert.AreEqual(4.5, response.Model.Rating);
        Assert.AreEqual(8, response.Model.RatingCount);
        Assert.IsNotNull(response.Model.CreatedAt);
        Assert.AreEqual("SDXL 1.0", response.Model.Version?.BaseModel);
        Assert.AreEqual(12345, response.Model.Version?.DownloadCount);
        Assert.AreEqual(6775783.6, response.Model.Version?.Files[0].SizeKb);
        Assert.AreEqual(
            "https://civitai.com/api/download/models/2053273",
            response.Model.Version?.Files[0].DownloadUrl
        );
        Assert.IsTrue(response.Model.Version?.Files[0].IsPrimary);
    }

    [TestMethod]
    public async Task ResolveFileUrlAsync_ReturnsLinkedVersionHref()
    {
        // /sha256/{hash} returns pageProps.models[] (plural) with full model data inside,
        // including version.href — which is the canonical URL we want to navigate to.
        const string sha256Json = """
            {"pageProps":{"id":"file-1","models":[{"id":878387,"name":"Stable Diffusion 3.5 Large","type":"Checkpoint","versions":[{"id":983602,"name":"Workflow","href":"/models/878387?modelVersionId=983602"}],"version":{"id":983309,"name":"Large","base_model":"SD 3.5 Large","href":"/models/878387?modelVersionId=983309"}}]}}
            """;

        var responses = new Queue<HttpResponseMessage>(
            [
                CreateJsonResponse("""<html><script>{"buildId":"test-build"}</script></html>""", "text/html"),
                CreateJsonResponse(sha256Json),
            ]
        );

        var client = CreateClient(new RecordingHandler((_, _) => responses.Dequeue()));
        var resolved = await client.ResolveFileUrlAsync(
            "/sha256/ffef7a279d9134626e6ce0d494fba84fc1c7e720b3c7df2d19a09dc3796d8f93"
        );

        // Prefer version.href (the version that actually contains this file) over versions[0].href.
        Assert.AreEqual("/models/878387?modelVersionId=983309", resolved);
    }

    [TestMethod]
    public async Task ResolveFileUrlAsync_NoLinkedModel_ReturnsNull()
    {
        // Orphaned hash with no linked models → should return null so the caller can
        // fall back to opening the URL externally instead of navigating to a dead page.
        const string sha256Json = """{"pageProps":{"id":"file-2","models":[]}}""";

        var responses = new Queue<HttpResponseMessage>(
            [
                CreateJsonResponse("""<html><script>{"buildId":"test-build"}</script></html>""", "text/html"),
                CreateJsonResponse(sha256Json),
            ]
        );

        var client = CreateClient(new RecordingHandler((_, _) => responses.Dequeue()));
        var resolved = await client.ResolveFileUrlAsync("/sha256/abc");

        Assert.IsNull(resolved);
    }

    private static ICivArchiveApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory
            .CreateClient()
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://civarchive.com") });

        return new CivArchiveApiClient(NullLogger<CivArchiveApiClient>.Instance, httpClientFactory);
    }

    private static HttpResponseMessage CreateJsonResponse(
        string content,
        string mediaType = "application/json"
    )
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType) },
            },
        };
    }

    private static string ListResponseJson(string resultsJson)
    {
        return "{\"pageProps\":{\"canonicalUrl\":\"https://civarchive.com/top-models\",\"data\":{\"results\":["
            + resultsJson
            + "],\"hits\":2,\"totalHits\":2},\"filters\":{\"q\":\"\",\"type\":\"all\",\"base_model\":\"all\",\"platform\":\"all\",\"sort\":\"top\",\"rating\":\"safe\",\"platform_status\":\"all\",\"kind\":\"all\",\"tags\":\"\",\"username\":\"\",\"period\":\"all\",\"page\":1},\"filterOptions\":{\"baseModels\":[\"Illustrious\",\"Pony\"],\"modelTypes\":[\"LORA\",\"Checkpoint\"]}}}";
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(responder(request, cancellationToken));
        }
    }
}
