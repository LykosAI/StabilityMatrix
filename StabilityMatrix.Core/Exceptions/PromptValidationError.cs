namespace StabilityMatrix.Core.Exceptions;

public class PromptValidationError : PromptError
{
    /// <inheritdoc />
    public PromptValidationError(string message, int textOffset, int textEndOffset)
        : base(message, textOffset, textEndOffset) { }

    public static PromptValidationError Network_UnknownType(int textOffset, int textEndOffset) =>
        new("Unknown network type", textOffset, textEndOffset);

    public static PromptValidationError Network_UnknownModel(
        string model,
        int textOffset,
        int textEndOffset
    ) => new($"Model '{model}' was not found locally", textOffset, textEndOffset);

    public static PromptSyntaxError Network_InvalidWeight(int textOffset, int textEndOffset) =>
        new("Invalid network weight, could not be parsed as double", textOffset, textEndOffset);
}
