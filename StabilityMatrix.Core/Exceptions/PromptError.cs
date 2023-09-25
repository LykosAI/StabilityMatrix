namespace StabilityMatrix.Core.Exceptions;

public abstract class PromptError : ApplicationException
{
    public int TextOffset { get; }
    public int TextEndOffset { get; }

    protected PromptError(string message, int textOffset, int textEndOffset) : base(message)
    {
        TextOffset = textOffset;
        TextEndOffset = textEndOffset;
    }
}
