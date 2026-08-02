using System.Text.Json;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class NullToDefaultJsonConverterTests
{
    [TestMethod]
    public void TestDeserialize_CivitStatsWithNulls_ShouldDefaultToZero()
    {
        // CivitAI started sending null for numeric stats fields (observed live 2026-08-01)
        const string json = """
            {
                "downloadCount": null,
                "ratingCount": null,
                "rating": null,
                "favoriteCount": null,
                "commentCount": null,
                "thumbsUpCount": null
            }
            """;

        var result = JsonSerializer.Deserialize<CivitModelStats>(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.DownloadCount);
        Assert.AreEqual(0, result.RatingCount);
        Assert.AreEqual(0d, result.Rating);
        Assert.AreEqual(0, result.FavoriteCount);
        Assert.AreEqual(0, result.CommentCount);
        Assert.AreEqual(0, result.ThumbsUpCount);
    }

    [TestMethod]
    public void TestDeserialize_CivitStatsWithValues_ShouldReadValues()
    {
        const string json = """
            {
                "downloadCount": 1234,
                "ratingCount": 56,
                "rating": 4.5,
                "favoriteCount": 7,
                "commentCount": 8,
                "thumbsUpCount": 90
            }
            """;

        var result = JsonSerializer.Deserialize<CivitModelStats>(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(1234, result.DownloadCount);
        Assert.AreEqual(56, result.RatingCount);
        Assert.AreEqual(4.5, result.Rating);
        Assert.AreEqual(7, result.FavoriteCount);
        Assert.AreEqual(8, result.CommentCount);
        Assert.AreEqual(90, result.ThumbsUpCount);
    }

    [TestMethod]
    public void TestSerialize_CivitStats_ShouldRoundTrip()
    {
        var stats = new CivitModelStats
        {
            DownloadCount = 42,
            Rating = 3.5,
            ThumbsUpCount = 5,
        };

        var json = JsonSerializer.Serialize(stats);
        var result = JsonSerializer.Deserialize<CivitModelStats>(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.DownloadCount);
        Assert.AreEqual(3.5, result.Rating);
        Assert.AreEqual(5, result.ThumbsUpCount);
    }
}
