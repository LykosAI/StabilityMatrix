using System.Text.RegularExpressions;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Processes;

public readonly record struct ProcessOutput
{
    /// <summary>
    /// Parsed text with escape sequences and line endings removed
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Optional Raw output,
    /// mainly for debug and logging.
    /// </summary>
    public string? RawText { get; init; }
    
    /// <summary>
    /// True if output from stderr, false for stdout.
    /// </summary>
    public bool IsStdErr { get; init; }
    
    /// <summary>
    /// Count of newlines to append to the output.
    /// (Currently not used)
    /// </summary>
    public int NewLines { get; init; }
    
    /// <summary>
    /// Instruction to clear last n lines
    /// From carriage return '\r'
    /// </summary>
    public int ClearLines { get; init; }
    
    /// <summary>
    /// Instruction to move write cursor up n lines
    /// From Ansi sequence ESC[#A where # is count of lines
    /// </summary>
    public int CursorUp { get; init; }
    
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
        
        // Normal parsing
        var originalText = text;
        
        // Remove \r from the beginning of the line, and add them to count
        var clearLines = 0;
        
        // Skip if starts with \r\n on windows
        if (!text.StartsWith(Environment.NewLine))
        {
            clearLines += text.CountStart('\r');
            text = text.TrimStart('\r');
        }
        
        // Also detect Ansi escape for cursor up, treat as clear lines also
        if (text.StartsWith("\u001b["))
        {
            var match = Regex.Match(text, @"\u001b\[(\d+?)A");
            if (match.Success)
            {
                // Default to 1 if no count
                var count = int.TryParse(match.Groups[1].Value, out var n) ? n : 1;
                // Add 1 to count to include current line
                clearLines += count + 1;
                // Set text to be after the escape sequence
                text = text[match.Length..];
            }
        }
        
        return new ProcessOutput
        {
            RawText = originalText,
            Text = text,
            IsStdErr = isStdErr,
            ClearLines = clearLines
        };
    }
}
