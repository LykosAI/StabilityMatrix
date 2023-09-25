using System.Diagnostics;
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
    public int CarriageReturn { get; init; }
    
    /// <summary>
    /// Instruction to move write cursor up n lines
    /// From Ansi sequence ESC[#A where # is count of lines
    /// </summary>
    public int CursorUp { get; init; }
    
    /// <summary>
    /// Flag-type Ansi commands
    /// </summary>
    public AnsiCommand AnsiCommand { get; init; }
    
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
        
        // Strip leading carriage return until newline
        while (!text.StartsWith(Environment.NewLine) && text.StartsWith('\r'))
        {
            text = text[1..];
            clearLines++;
        }
        
        // Detect ansi escape sequences
        var ansiCommands = AnsiCommand.None;
        var cursorUp = 0;
        
        if (text.Contains('\u001b'))
        {
            // Cursor up sequence - ESC[#A
            // Where # is count of lines to move up, if not specified, default to 1
            if (Regex.Match(text, @"\x1B\[(\d+)?A") is {Success: true} match)
            {
                // Default to 1 if no count
                cursorUp = int.TryParse(match.Groups[1].Value, out var n) ? n : 1;
                
                // Remove the sequence from the text
                text = text[..match.Index] + text[(match.Index + match.Length)..];
            }
            // Erase line sequence - ESC[#K
            // (For erasing we don't move the cursor)
            // Omitted - defaults to 0
            // 0 - clear from cursor to end of line
            // 1 - clear from start of line to cursor
            // 2 - clear entire line
            if (Regex.Match(text, @"\x1B\[(0|1|2)?K") is {Success: true} match2)
            {
                // Default to 0 if no count
                var eraseLineMode = int.TryParse(match2.Groups[1].Value, out var n) ? n : 0;
                
                ansiCommands |= eraseLineMode switch
                {
                    0 => AnsiCommand.EraseToEndOfLine,
                    1 => AnsiCommand.EraseFromStartOfLine,
                    2 => AnsiCommand.EraseLine,
                    _ => AnsiCommand.None
                };
                
                // Remove the sequence from the text
                text = text[..match2.Index] + text[(match2.Index + match2.Length)..];
            }
            // Private modes, all of these can be safely ignored
            if (Regex.Match(text, @"\x1B\[?(25l|25h|47l|47h|1049h|1049l)") is
                     {Success: true} match3)
            {
                // Remove the sequence from the text
                text = text[..match3.Index] + text[(match3.Index + match3.Length)..];
            }
        }
        
        // If text still contains escape sequences, remove them
        if (text.Contains('\u001b'))
        {
            Debug.WriteLine($"Removing unhandled escape sequences: {text.ToRepr()}");
            text = AnsiParser.AnsiEscapeSequenceRegex().Replace(text, "");
        }
        
        var output = new ProcessOutput
        {
            RawText = originalText,
            Text = text,
            IsStdErr = isStdErr,
            CarriageReturn = clearLines,
            CursorUp = cursorUp,
            AnsiCommand = ansiCommands,
        };
        return output;
    }
}
