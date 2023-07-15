namespace StabilityMatrix.Core.Processes;

public readonly record struct ProcessOutput
{
    /// <summary>
    /// Parsed text with escape sequences and line endings removed
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Raw output
    /// </summary>
    public string? RawText { get; init; }
    
    /// <summary>
    /// True if output from stderr, false for stdout
    /// </summary>
    public bool IsStdErr { get; init; }
    
    /// <summary>
    /// Count of newlines to append to the output
    /// </summary>
    public int NewLines { get; init; }
    
    /// <summary>
    /// Instruction to clear last n lines
    /// </summary>
    public int ClearLines { get; init; }
    
    /// <summary>
    /// Apc message sent from the subprocess
    /// </summary>
    public ApcMessage? ApcMessage { get; init; }

    public static ProcessOutput FromStdOutLine(string text)
    {
        return FromLine(text, false);
    }
    
    public static ProcessOutput FromStdErrLine(string text)
    {
        return FromLine(text, true);
    }

    private static ProcessOutput FromLine(string text, bool isStdErr)
    {
        // Parse APC message
        if (ApcParser.TryParse(text, out var message))
        {
            // Override and return
            return new ProcessOutput
            {
                RawText = text,
                Text = text,
                IsStdErr = isStdErr,
                ApcMessage = message
            };
        }
        // If text contains newlines, split it first
        if (text.Contains(Environment.NewLine))
        {
            var lines = text.Split(Environment.NewLine);
            // Now take the first line and trim \r
            var firstLineLength = lines[0].Length;
            lines[0] = lines[0].TrimStart('\r');
            var crCount = firstLineLength - lines[0].Length;
            // Join them back together
            var result = string.Join(Environment.NewLine, lines);
            return new ProcessOutput
            {
                RawText = text,
                Text = result,
                IsStdErr = isStdErr,
                ClearLines = crCount
            };
        }
        else
        {
            // If no newlines, just trim \r
            var trimmed = text.TrimStart('\r');
            var crCount = text.Length - trimmed.Length;
            return new ProcessOutput
            {
                RawText = text,
                Text = trimmed,
                IsStdErr = isStdErr,
                ClearLines = crCount
            };
        }
    }
}
