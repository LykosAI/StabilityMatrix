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

    /// <summary>
    /// Error for a token the prompt grammar marks as <c>invalid.illegal</c>.
    /// </summary>
    /// <param name="scopes">The token's TextMate scopes, used to pick the message.</param>
    public static PromptSyntaxError InvalidToken(
        IReadOnlyCollection<string> scopes,
        int textOffset,
        int textEndOffset
    )
    {
        var message =
            scopes.Contains("invalid.illegal.mismatched.parenthesis.closing.prompt")
                ? "Mismatched closing parenthesis ')'"
            : scopes.Contains("invalid.illegal.mismatched.parenthesis.opening.prompt")
                ? "Mismatched opening parenthesis '('"
            : scopes.Contains("invalid.illegal.expected-weight-separator.prompt") ? "Expected numeric weight"
            : "Invalid token";

        return new PromptSyntaxError(message, textOffset, textEndOffset);
    }

    /// <inheritdoc />
    public PromptSyntaxError(string message, int textOffset, int textEndOffset)
        : base(message, textOffset, textEndOffset) { }
}
