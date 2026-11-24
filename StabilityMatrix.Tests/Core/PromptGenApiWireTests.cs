using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Api.PromptGenApi;

namespace StabilityMatrix.Tests.Core;

/// <summary>
/// Deserialises a response shaped like what promptgen.lykos.ai actually returns for
/// <c>/expand-prompt</c> through the generated client's models. Guards against the swagger
/// describing a field differently from the bytes on the wire, which the generated code cannot
/// detect on its own.
/// </summary>
[TestClass]
public class PromptGenApiWireTests
{
    // The enum and casing settings the app's Refit clients use (App.ConfigureServices).
    private static readonly JsonSerializerOptions AppOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const string LiveShapedResponse = """
        {
          "remainingBalance": 246.5,
          "operationId": "c3293462-55a8-4ff2-87fd-822ce9570864",
          "response": { "positivePrompt": "a white wolf under aurora", "negativePrompt": "blurry" },
          "inputTokens": 512,
          "outputTokens": 96,
          "calculatedCost": 0.0031,
          "chargedAmount": 1,
          "currency": "USD",
          "inputTokensDetails": [ { "modality": "Text", "tokenCount": 512 } ],
          "outputTokensDetails": [ { "modality": "Text", "tokenCount": 96 } ],
          "availableTokens": 246
        }
        """;

    [TestMethod]
    public void ExpandPromptResponse_DeserializesTheLiveShape()
    {
        var response = JsonSerializer.Deserialize<PromptExpansionResponse>(LiveShapedResponse, AppOptions);

        Assert.IsNotNull(response);
        Assert.AreEqual(246.5, response.RemainingBalance);
        Assert.AreEqual("a white wolf under aurora", response.Response.PositivePrompt);
        Assert.AreEqual(512, response.InputTokens);
        Assert.AreEqual("Text", response.InputTokensDetails.Single().Modality.ToString());
        Assert.AreEqual(96, response.OutputTokensDetails.Single().TokenCount);
    }

    [TestMethod]
    public void ExpandPromptResponse_ToleratesMissingTokenDetails()
    {
        const string minimal = """
            { "remainingBalance": 3, "operationId": "op", "response": { "positivePrompt": "p" } }
            """;

        var response = JsonSerializer.Deserialize<PromptExpansionResponse>(minimal, AppOptions);

        Assert.IsNotNull(response);
        Assert.AreEqual(3, response.RemainingBalance);
        Assert.IsNull(response.InputTokensDetails);
    }
}
