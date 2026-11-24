using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Api.PromptGenApi;

/// <summary>
/// Spark model family served by the PromptGen API's <c>/expand-prompt</c> endpoint.
/// Values are persisted in Inference project files, so they must keep their numbers.
/// </summary>
public enum PromptAmplifierModel
{
    [StringValue("Spark Classic")]
    SparkClassic = 0,

    [StringValue("Spark v2.0")]
    SparkV2 = 1,
}

public static class PromptAmplifierModelExtensions
{
    /// <summary>
    /// Resolves the PromptGen model configuration name for this family, with or without
    /// extended thinking. These are the server's <c>ChatCompletionConfigs</c> keys and are
    /// case-sensitive.
    /// </summary>
    public static string ToPromptGenModelName(this PromptAmplifierModel model, bool isThinkingEnabled) =>
        (model, isThinkingEnabled) switch
        {
            (PromptAmplifierModel.SparkClassic, false) => "PromptV1Dev",
            (PromptAmplifierModel.SparkClassic, true) => "PromptV1ThinkingDev",
            (PromptAmplifierModel.SparkV2, false) => "PromptV2Dev",
            (PromptAmplifierModel.SparkV2, true) => "PromptV2ThinkingDev",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null),
        };
}
