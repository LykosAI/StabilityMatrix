namespace StabilityMatrix.Core.Exceptions;

public class PromptSyntaxError : PromptError
{
    public static PromptSyntaxError Network_ExpectedSeparator(int textOffset, int textEndOffset) =>
        new("Expected separator", textOffset, textEndOffset);
    
    public static PromptSyntaxError Network_ExpectedType(int textOffset, int textEndOffset) =>
        new("Expected network type", textOffset, textEndOffset);
    
    public static PromptSyntaxError Network_ExpectedName(int textOffset, int textEndOffset) =>
        new("Expected network name", textOffset, textEndOffset);
    
    public static PromptSyntaxError Network_ExpectedWeight(int textOffset, int textEndOffset) =>
        new("Expected network weight", textOffset, textEndOffset);
    
    public static PromptSyntaxError UnexpectedEndOfText(int textOffset, int textEndOffset) =>
        new("Unexpected end of text", textOffset, textEndOffset);

    /// <inheritdoc />
    public PromptSyntaxError(string message, int textOffset, int textEndOffset) : base(message, textOffset, textEndOffset)
    {
    }
}
