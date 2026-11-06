using System.Text.Json;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class CivitImageTests
{
    [TestMethod]
    public void TestDeserialize_CivitImageWithNullDimensions_ShouldBeNull()
    {
        // CivitAI sends null width/height for some images (observed live 2026-11-06, issue #1792)
        const string json = """
            {
                "url": "https://image.civitai.com/example.jpeg",
                "nsfwLevel": 8,
                "width": null,
                "height": null,
                "hash": "abc",
                "type": "image"
            }
            """;

        var result = JsonSerializer.Deserialize<CivitImage>(json);

        Assert.IsNotNull(result);
        Assert.IsNull(result.Width);
        Assert.IsNull(result.Height);
        Assert.AreEqual("https://image.civitai.com/example.jpeg", result.Url);
    }

    [TestMethod]
    public void TestDeserialize_CivitImageWithDimensions_ShouldReadValues()
    {
        const string json = """
            { "url": "https://image.civitai.com/example.jpeg", "width": 832, "height": 1216 }
            """;

        var result = JsonSerializer.Deserialize<CivitImage>(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(832, result.Width);
        Assert.AreEqual(1216, result.Height);
    }

    [TestMethod]
    public void TestDeserialize_ModelsResponseWithNullImageDimensions_ShouldKeepAllItems()
    {
        // One image with null dimensions used to fail the whole page of results
        const string json = """
            {
                "items": [
                    {
                        "id": 1,
                        "name": "First",
                        "modelVersions": [
                            { "id": 10, "name": "v1", "images": [ { "url": "https://a", "width": 512, "height": 512 } ] }
                        ]
                    },
                    {
                        "id": 2,
                        "name": "Second",
                        "modelVersions": [
                            { "id": 20, "name": "v1", "images": [ { "url": "https://b", "width": null, "height": null } ] }
                        ]
                    }
                ],
                "metadata": {}
            }
            """;

        var result = JsonSerializer.Deserialize<CivitModelsResponse>(json);

        Assert.IsNotNull(result?.Items);
        Assert.AreEqual(2, result.Items.Count);
        Assert.IsNull(result.Items[1].ModelVersions![0].Images![0].Width);
    }
}
