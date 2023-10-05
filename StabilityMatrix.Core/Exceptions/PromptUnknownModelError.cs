using StabilityMatrix.Core.Models.Tokens;

namespace StabilityMatrix.Core.Exceptions;

public class PromptUnknownModelError : PromptValidationError
{
    public string ModelName { get; }

    public PromptExtraNetworkType ModelType { get; }

    /// <inheritdoc />
    public PromptUnknownModelError(
        string message,
        int textOffset,
        int textEndOffset,
        string modelName,
        PromptExtraNetworkType modelType
    )
        : base(message, textOffset, textEndOffset)
    {
        ModelName = modelName;
        ModelType = modelType;
    }
}
