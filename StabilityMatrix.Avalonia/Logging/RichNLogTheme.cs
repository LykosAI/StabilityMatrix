using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using NLog;
using NLog.MessageTemplates;
using NLog.Targets;

namespace StabilityMatrix.Avalonia.Logging;

// Define our own theme styles, inspired by Serilog.Expressions
public enum RichNLogThemeStyle
{
    /// <summary>
    /// Foreground for main message text.
    /// </summary>
    Text,

    /// <summary>
    /// Foreground for logger name.
    /// </summary>
    LogArea,

    /// <summary>
    /// Boilerplate like timestamp, logger name parts
    /// </summary>
    SecondaryText,
    TertiaryText,

    /// <summary>
    /// For errors, missing values
    /// </summary>
    Invalid,
    Null,
    Name,
    String,
    Number,
    Boolean,
    Scalar,
    LevelVerbose,
    LevelDebug,
    LevelInformation,
    LevelWarning,
    LevelError,
    LevelFatal,
    // Add more specific styles if needed, e.g., SourceContext, RequestPath, etc.
}

[Localizable(false)]
public class RichNLogTheme
{
    // Map styles to ANSI codes (e.g. "\x1b[38;5;0253m")
    public Dictionary<RichNLogThemeStyle, string> Styles { get; set; } = new();

    // Method to get style, falling back to default if not found
    public string GetAnsiStyle(RichNLogThemeStyle style) =>
        Styles.TryGetValue(style, out var ansi) ? ansi : string.Empty;

    public void ApplyStyle(TextWriter writer, RichNLogThemeStyle style, string text)
    {
        var ansi = GetAnsiStyle(style);
        if (!string.IsNullOrEmpty(ansi))
            writer.Write(ansi);
        writer.Write(text);
        if (!string.IsNullOrEmpty(ansi))
            writer.Write(AnsiReset);
    }

    // Using ANSI 256-color codes (like \x1b[38;5;XXXm)
    // Reference: https://gist.github.com/fnky/458719343aabd01cfb17a3a4f7296797 (ANSI codes)

    public static RichNLogTheme Simple { get; } =
        new RichNLogTheme
        {
            Styles = new Dictionary<RichNLogThemeStyle, string>
            {
                // --- Log Level Styles ---
                [RichNLogThemeStyle.LevelVerbose] = "\e[37m", // White/Light Gray (Subtle)
                [RichNLogThemeStyle.LevelDebug] = "\e[32;1m", // Bold Green
                [RichNLogThemeStyle.LevelInformation] = "\e[36;1m", // Bold Cyan
                [RichNLogThemeStyle.LevelWarning] = "\e[33;1m", // Bold Yellow
                [RichNLogThemeStyle.LevelError] = "\e[31;1m", // Bold Red
                [RichNLogThemeStyle.LevelFatal] = "\e[31;1m\e[48;5;0238m", // Bold black on red background
                // --- Value Styles ---
                [RichNLogThemeStyle.Null] = "\e[34m", // Blue
                [RichNLogThemeStyle.Name] = "\e[37m", // White (or default)
                [RichNLogThemeStyle.String] = "\e[36m", // Cyan
                [RichNLogThemeStyle.Number] = "\e[35m", // Magenta
                [RichNLogThemeStyle.Boolean] = "\e[34m", // Blue
                [RichNLogThemeStyle.Scalar] = "\e[38;5;0079m", // Purple/Magenta
                // --- Text Styles ---
                [RichNLogThemeStyle.Text] = "\e[38;5;0253m", // Near white
                [RichNLogThemeStyle.SecondaryText] = "\e[38;5;0246m", // Medium gray
                [RichNLogThemeStyle.TertiaryText] = "\e[90m", // Bright Black (Gray)
            },
        };
    public static RichNLogTheme Default { get; } =
        new RichNLogTheme
        {
            Styles = new Dictionary<RichNLogThemeStyle, string>
            {
                // --- Text Styles ---
                { RichNLogThemeStyle.Text, "\e[38;5;0253m" }, // Near white
                { RichNLogThemeStyle.SecondaryText, "\e[38;5;0246m" }, // Medium gray (Boilerplate like timestamp, logger name parts)
                { RichNLogThemeStyle.TertiaryText, "\e[38;5;0242m" }, // Darker gray (Punctuation like braces, colons, commas)
                { RichNLogThemeStyle.Invalid, "\e[33;1m" }, // Bold Yellow (For errors, missing values - standard ANSI bright)
                // --- Value Styles ---
                { RichNLogThemeStyle.Null, "\e[38;5;0038m" }, // Blue/Purple (For `null` keyword)
                { RichNLogThemeStyle.Name, "\e[37m" }, // Light Cyan/Blue (Property names, logger name)
                { RichNLogThemeStyle.String, "\e[38;5;0216m" }, // Orange/Pink (String literals)
                { RichNLogThemeStyle.Number, "\e[38;5;151m" }, // Light Green (Numeric values)
                { RichNLogThemeStyle.Boolean, "\e[38;5;0038m" }, // Blue/Purple (For `true`/`false`)
                { RichNLogThemeStyle.Scalar, "\e[38;5;0079m" }, // Purple/Magenta (Other scalar types like Guid, DateTime)
                // --- Log Level Styles ---
                { RichNLogThemeStyle.LevelVerbose, "\e[37m" }, // White/Light Gray (Subtle)
                { RichNLogThemeStyle.LevelDebug, "\e[37m" }, // White/Light Gray (Subtle) - Same as Verbose
                { RichNLogThemeStyle.LevelInformation, "\e[37;1m" }, // Bold White (Default, noticeable)
                { RichNLogThemeStyle.LevelWarning, "\e[38;5;0229m" }, // Bright Yellow/Gold
                { RichNLogThemeStyle.LevelError, "\e[38;5;0197m" }, // Bright Red foreground
                { RichNLogThemeStyle.LevelFatal, "\e[38;5;0197m\e[48;5;0238m" }, // Bright Red foreground on dark gray background

                // --- Custom Semantic Styles (Optional examples) ---
                // { RichNLogThemeStyle.SourceContext, "\x1b[38;5;0248m" }, // Example: slightly different gray for source context
                // { RichNLogThemeStyle.HttpRequest,   "\x1b[38;5;0111m" }, // Example: a distinct color for HTTP request paths
            },
        };

    public static RichNLogTheme Code { get; } =
        new()
        {
            Styles = new Dictionary<RichNLogThemeStyle, string>
            {
                [RichNLogThemeStyle.Text] = "\e[38;5;0253m",
                [RichNLogThemeStyle.SecondaryText] = "\e[38;5;0246m",
                [RichNLogThemeStyle.TertiaryText] = "\e[38;5;0242m",
                [RichNLogThemeStyle.Invalid] = "\e[33;1m",
                [RichNLogThemeStyle.Null] = "\e[38;5;0038m",
                [RichNLogThemeStyle.Name] = "\e[38;5;0081m",
                [RichNLogThemeStyle.String] = "\e[38;5;0216m",
                [RichNLogThemeStyle.Number] = "\e[38;5;151m",
                [RichNLogThemeStyle.Boolean] = "\e[38;5;0038m",
                [RichNLogThemeStyle.Scalar] = "\e[38;5;0079m",
                [RichNLogThemeStyle.LevelVerbose] = "\e[38;5;8m",
                [RichNLogThemeStyle.LevelDebug] = "\e[37m",
                [RichNLogThemeStyle.LevelInformation] = "\e[37;1m",
                [RichNLogThemeStyle.LevelWarning] = "\e[38;5;0229m",
                [RichNLogThemeStyle.LevelError] = "\e[38;5;0197m\e[48;5;0238m",
                [RichNLogThemeStyle.LevelFatal] = "\e[38;5;0197m\e[48;5;0238m",
            },
        };

    public static RichNLogTheme Code2 { get; } =
        new()
        {
            Styles = new Dictionary<RichNLogThemeStyle, string>
            {
                [RichNLogThemeStyle.Text] = "\e[38;5;0015m",
                [RichNLogThemeStyle.SecondaryText] = "\e[38;5;0246m",
                [RichNLogThemeStyle.TertiaryText] = "\e[38;5;0242m",
                [RichNLogThemeStyle.LogArea] = "\e[37;1m",
                [RichNLogThemeStyle.Invalid] = "\e[33;1m",
                [RichNLogThemeStyle.Null] = "\e[38;5;0038m",
                [RichNLogThemeStyle.Name] = "\e[38;5;0081m",
                [RichNLogThemeStyle.String] = "\e[38;5;0216m",
                [RichNLogThemeStyle.Number] = "\e[38;5;151m",
                [RichNLogThemeStyle.Boolean] = "\e[38;5;0038m",
                [RichNLogThemeStyle.Scalar] = "\e[38;5;0079m",
                // --- Log Level Styles ---
                [RichNLogThemeStyle.LevelVerbose] = "\e[37m", // White/Light Gray (Subtle)
                [RichNLogThemeStyle.LevelDebug] = "\e[32;1m", // Bold Green
                [RichNLogThemeStyle.LevelInformation] = "\e[36;1m", // Bold Cyan
                [RichNLogThemeStyle.LevelWarning] = "\e[33;1m", // Bold Yellow
                [RichNLogThemeStyle.LevelError] = "\e[31;1m", // Bold Red
                [RichNLogThemeStyle.LevelFatal] = "\e[31;1m\e[48;5;0238m", // Bold black on red background
            },
        };

    public static RichNLogTheme Literate { get; } =
        new()
        {
            Styles = new Dictionary<RichNLogThemeStyle, string>
            {
                [RichNLogThemeStyle.Text] = "\e[38;5;0015m",
                [RichNLogThemeStyle.SecondaryText] = "\e[38;5;0007m",
                [RichNLogThemeStyle.TertiaryText] = "\e[38;5;0008m",
                [RichNLogThemeStyle.Invalid] = "\e[38;5;0011m",
                [RichNLogThemeStyle.Null] = "\e[38;5;0027m",
                [RichNLogThemeStyle.Name] = "\e[38;5;0007m",
                [RichNLogThemeStyle.String] = "\e[38;5;0045m",
                [RichNLogThemeStyle.Number] = "\e[38;5;0200m",
                [RichNLogThemeStyle.Boolean] = "\e[38;5;0027m",
                [RichNLogThemeStyle.Scalar] = "\e[38;5;0085m",
                [RichNLogThemeStyle.LevelVerbose] = "\e[38;5;0007m",
                [RichNLogThemeStyle.LevelDebug] = "\e[38;5;0007m",
                [RichNLogThemeStyle.LevelInformation] = "\e[38;5;0015m",
                [RichNLogThemeStyle.LevelWarning] = "\e[38;5;0011m",
                [RichNLogThemeStyle.LevelError] = "\e[38;5;0015m\e[48;5;0196m",
                [RichNLogThemeStyle.LevelFatal] = "\e[38;5;0015m\e[48;5;0196m",
            },
        };

    public const string AnsiReset = "\e[0m";
}

[Localizable(false)]
[Target("RichConsole")]
public sealed class RichConsoleTarget : TargetWithLayout
{
    public RichNLogTheme Theme { get; set; } = RichNLogTheme.Default; // Allow theme customization

    public RichConsoleTarget()
        : this("RichConsoleTarget") { }

    public RichConsoleTarget(string name)
    {
        Name = name;
    }

    protected override void Write(LogEventInfo logEvent)
    {
        // Using Console.Out directly for simplicity. Consider locking or async for production.
        var writer = Console.Out;
        var theme = Theme; // Use the configured theme

        // --- Timestamp ---
        ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, logEvent.TimeStamp.ToString("HH:mm:ss.fff"));
        writer.Write(" ");

        // --- Level ---
        RenderLevel(writer, logEvent.Level);
        writer.Write(" ");

        // --- Logger Name ---
        var formattedLoggerName = logEvent.LoggerName?.Split(".").Last();
        if (formattedLoggerName != null)
        {
            ApplyStyle(writer, RichNLogThemeStyle.LogArea, formattedLoggerName);
            ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ":");
            writer.Write(" ");
        }

        // --- Message Template and Parameters ---
        RenderMessage(writer, logEvent, theme);
        writer.Write(" ");

        // --- Properties (Similar to Layout.FromMethod, but with styling) ---
        // Exclude any already rendered properties
        /*var templateParamNames = logEvent
            .MessageTemplateParameters
            .Select(p => p.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet();
        var renderProperties = logEvent
            .Properties
            .Where(p => p.Key is string && !templateParamNames.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value);

        RenderPropertiesBlock(writer, renderProperties, theme);*/
        // RenderProperties(writer, renderProperties);

        // --- Exception ---
        if (logEvent.Exception != null)
        {
            writer.WriteLine();
            // Potentially style different parts of the exception/stack trace
            ApplyStyle(writer, RichNLogThemeStyle.Invalid, logEvent.Exception.ToString());
        }

        writer.WriteLine(); // Final newline for the log entry
    }

    private void RenderLevel(TextWriter writer, LogLevel level)
    {
        RichNLogThemeStyle style;
        string levelText;

        if (level == LogLevel.Trace)
        {
            style = RichNLogThemeStyle.LevelVerbose;
            levelText = "TRACE";
        }
        else if (level == LogLevel.Debug)
        {
            style = RichNLogThemeStyle.LevelDebug;
            levelText = "DEBUG";
        }
        else if (level == LogLevel.Info)
        {
            style = RichNLogThemeStyle.LevelInformation;
            levelText = "INFO";
        }
        else if (level == LogLevel.Warn)
        {
            style = RichNLogThemeStyle.LevelWarning;
            levelText = "WARN";
        }
        else if (level == LogLevel.Error)
        {
            style = RichNLogThemeStyle.LevelError;
            levelText = "ERROR";
        }
        else if (level == LogLevel.Fatal)
        {
            style = RichNLogThemeStyle.LevelFatal;
            levelText = "FATAL";
        }
        else
        {
            style = RichNLogThemeStyle.SecondaryText;
            levelText = level.ToString().ToUpperInvariant();
        }

        // Fixed width
        levelText = levelText.PadRight(5);

        ApplyStyle(writer, style, levelText);
    }

    /*private void RenderMessage(TextWriter writer, LogEventInfo logEvent)
    {
        var template = logEvent.Message ?? logEvent.FormattedMessage ?? ""; // Fallback
        var properties = logEvent.Properties; // Primarily use properties for named lookup

        var lastIndex = 0;
        while (lastIndex < template.Length)
        {
            var openBrace = template.IndexOf('{', lastIndex);

            // End of template or no more placeholders
            if (openBrace == -1)
            {
                if (lastIndex < template.Length)
                {
                    ApplyStyle(writer, RichNLogThemeStyle.Text, template.Substring(lastIndex));
                }
                break;
            }

            // Handle escaped brace {{
            if (openBrace + 1 < template.Length && template[openBrace + 1] == '{')
            {
                ApplyStyle(
                    writer,
                    RichNLogThemeStyle.Text,
                    template.Substring(lastIndex, openBrace - lastIndex + 1)
                ); // Render text + single {
                lastIndex = openBrace + 2; // Skip {{
                continue;
            }

            // Render literal text before the placeholder
            if (openBrace > lastIndex)
            {
                ApplyStyle(
                    writer,
                    RichNLogThemeStyle.Text,
                    template.Substring(lastIndex, openBrace - lastIndex)
                );
            }

            // Find the matching closing brace }
            var closeBraceScan = openBrace + 1;
            var closeBrace = -1;
            var braceDepth = 0; // Handle nested braces within format specifiers if needed (simple case here)
            while (closeBraceScan < template.Length)
            {
                if (
                    closeBraceScan + 1 < template.Length
                    && template[closeBraceScan] == '}'
                    && template[closeBraceScan + 1] == '}'
                )
                {
                    // Skip escaped }}
                    closeBraceScan += 2;
                    continue;
                }

                if (template[closeBraceScan] == '{')
                    braceDepth++;
                else if (template[closeBraceScan] == '}')
                {
                    if (braceDepth == 0)
                    {
                        closeBrace = closeBraceScan;
                        break;
                    }
                    braceDepth--;
                }
                closeBraceScan++;
            }

            if (closeBrace == -1) // Malformed - no closing brace found
            {
                ApplyStyle(writer, RichNLogThemeStyle.Invalid, template.Substring(openBrace)); // Render rest as invalid
                lastIndex = template.Length;
                break;
            }

            // Extract placeholder content (e.g., "UserId:N5" or "0,10")
            var placeholderContent = template.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Basic parsing for name, format, and alignment (alignment not used here yet)
            var name = placeholderContent;
            string? format = null;
            // int? alignment = null; // Alignment parsing would go here

            var formatColon = placeholderContent.IndexOf(':');
            var alignmentComma = placeholderContent.IndexOf(','); // Handle alignment separator ','

            if (formatColon != -1 && (alignmentComma == -1 || formatColon < alignmentComma)) // Colon before comma or no comma
            {
                name = placeholderContent.Substring(0, formatColon);
                format = placeholderContent.Substring(formatColon + 1);
                // Further parse alignment out of 'name' if comma exists before colon
                if (alignmentComma != -1 && alignmentComma < formatColon)
                {
                    name = placeholderContent.Substring(0, alignmentComma);
                    // Parse alignment value if needed
                }
            }
            else if (alignmentComma != -1) // Comma but no colon, or comma before colon
            {
                name = placeholderContent.Substring(0, alignmentComma);
                // Parse alignment value if needed
                // format might be after comma if no colon, or just alignment exists
            }

            // Attempt to find the value primarily using the name from Properties
            object? value = null;
            var valueFound = false;
            if (properties != null && properties.TryGetValue(name, out var propValue))
            {
                value = propValue;
                valueFound = true;
            }
            // Add positional fallback if needed (e.g., if name is purely numeric and not in properties)
            else if (
                int.TryParse(name, out var positionalIndex)
                && logEvent.Parameters != null
                && positionalIndex >= 0
                && positionalIndex < logEvent.Parameters.Length
            )
            {
                value = logEvent.Parameters[positionalIndex];
                valueFound = true;
            }

            // Render the value using styling and format
            if (valueFound)
            {
                RenderPropertyValue(writer, value, format);
            }
            else // Value not found - render placeholder text as invalid/missing
            {
                ApplyStyle(writer, RichNLogThemeStyle.Invalid, $"{{{placeholderContent}}}");
            }

            lastIndex = closeBrace + 1; // Move past the processed placeholder
        }
    }*/

    // --- Render Message (Parsing Template) ---
    private void RenderMessage(TextWriter writer, LogEventInfo logEvent, RichNLogTheme theme)
    {
        var template = logEvent.Message ?? logEvent.FormattedMessage ?? "";
        var properties = logEvent.Properties;

        var lastIndex = 0;
        // Keep track of positional parameter index if needed for fallback
        // int positionalParamIndex = 0;

        while (lastIndex < template.Length)
        {
            var openBrace = template.IndexOf('{', lastIndex);
            if (openBrace == -1)
            {
                if (lastIndex < template.Length)
                    theme.ApplyStyle(writer, RichNLogThemeStyle.Text, template.Substring(lastIndex));
                break;
            }

            if (openBrace + 1 < template.Length && template[openBrace + 1] == '{') // Escaped {{
            {
                theme.ApplyStyle(
                    writer,
                    RichNLogThemeStyle.Text,
                    template.Substring(lastIndex, openBrace - lastIndex + 1)
                );
                lastIndex = openBrace + 2;
                continue;
            }

            if (openBrace > lastIndex)
                theme.ApplyStyle(
                    writer,
                    RichNLogThemeStyle.Text,
                    template.Substring(lastIndex, openBrace - lastIndex)
                );

            var closeBrace = FindClosingBrace(template, openBrace + 1);

            if (closeBrace == -1) // Malformed
            {
                theme.ApplyStyle(writer, RichNLogThemeStyle.Invalid, template.Substring(openBrace));
                lastIndex = template.Length;
                break;
            }

            var placeholderContent = template.Substring(openBrace + 1, closeBrace - openBrace - 1);
            ParsePlaceholder(placeholderContent, out var name, out var format, out var captureType); // Pass captureType out

            object? value = null;
            var valueFound = properties != null && properties.TryGetValue(name, out value);
            // Add positional fallback logic here if required, using logEvent.Parameters and positionalParamIndex

            if (valueFound)
            {
                // Pass captureType and format to RenderPropertyValue
                RenderPropertyValue(writer, value, theme, format, captureType);
            }
            else
            {
                theme.ApplyStyle(writer, RichNLogThemeStyle.Invalid, $"{{{placeholderContent}}}");
            }

            lastIndex = closeBrace + 1;
            // if (valueFound && IsPositional(name)) positionalParamIndex++; // Increment if positional logic used
        }
    }

    private static void ParsePlaceholder(
        string content,
        out string name,
        out string? format,
        out CaptureType captureType
    )
    {
        // Defaults
        captureType = CaptureType.Normal;
        name = content; // Assume full content is name initially
        format = null;

        if (string.IsNullOrEmpty(content))
            return; // Empty placeholder? Handle gracefully.

        var nameStart = 0;

        // Check for capture type sigil at the beginning
        if (content[0] == '@')
        {
            captureType = CaptureType.Serialize;
            nameStart = 1;
        }
        else if (content[0] == '$')
        {
            captureType = CaptureType.Stringify;
            nameStart = 1;
        }
        // No sigil means CaptureType.Normal

        // Find format/alignment after the name part
        var formatColon = -1;
        var alignmentComma = -1;

        for (var i = nameStart; i < content.Length; ++i)
        {
            if (content[i] == ':')
            {
                formatColon = i;
                break; // Found format first
            }

            if (content[i] == ',')
            {
                alignmentComma = i;
                // Don't break, format might come after alignment
            }
        }

        var nameEnd = content.Length; // Default end of name is end of content

        if (formatColon != -1 && (alignmentComma == -1 || formatColon < alignmentComma))
        {
            // Format found, and it comes before any alignment or there's no alignment
            nameEnd = formatColon;
            format = content[(formatColon + 1)..];
        }
        else if (alignmentComma != -1)
        {
            // Alignment found, and it comes before any format or there's no format
            nameEnd = alignmentComma;
            // Format might be after alignment comma if no colon was found earlier
            if (formatColon == -1 && alignmentComma + 1 < content.Length)
            {
                // format = content.Substring(alignmentComma + 1); // Basic assumption
            }
            // TODO: Handle parsing the alignment value itself if needed
        }

        // Extract the name based on start index and calculated end
        name = content.Substring(nameStart, nameEnd - nameStart);
    }

    /// <summary>
    /// Finds the index of the matching closing brace '}' for an opening brace '{',
    /// starting the search from a specified position within a string template.
    /// Correctly handles nested braces and escaped closing braces ('}}').
    /// </summary>
    /// <param name="template">The string template to search within.</param>
    /// <param name="startIndex">The index within the template immediately after the opening brace for which the corresponding closing brace should be found.</param>
    /// <returns>The index of the matching closing brace, or -1 if no matching closing brace is found before the end of the template.</returns>
    private static int FindClosingBrace(string template, int startIndex)
    {
        var braceDepth = 0;
        for (var i = startIndex; i < template.Length; ++i)
        {
            if (i + 1 < template.Length && template[i] == '}' && template[i + 1] == '}')
            {
                i++; // Skip escaped }}
                continue;
            }

            if (template[i] == '{')
                braceDepth++;
            else if (template[i] == '}')
            {
                if (braceDepth == 0)
                    return i;
                braceDepth--;
            }
        }

        return -1; // Not found
    }

    private void RenderProperties(TextWriter writer, IDictionary<object, object> properties)
    {
        // Filter out internal/unwanted properties if needed
        var propsToRender = properties.Where(p => p.Key is string key && key != "CallerMemberName").ToList();

        if (!propsToRender.Any())
            return;

        writer.Write(" "); // Space before properties start
        ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "{ "); // Opening brace

        var firstProp = true;
        foreach (var prop in propsToRender)
        {
            if (!firstProp)
                ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ", ");
            firstProp = false;

            // Style the property name
            ApplyStyle(writer, RichNLogThemeStyle.Name, prop.Key.ToString() ?? "null");
            ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "=");

            // Style the property value based on its type
            RenderPropertyValue(writer, prop.Value);
        }

        ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, " }"); // Closing brace
    }

    // --- Render Properties Block (Optional alternative to inline rendering) ---
    private void RenderPropertiesBlock(
        TextWriter writer,
        IReadOnlyDictionary<object, object> properties,
        RichNLogTheme theme
    )
    {
        var propsToRender = properties
            ?.Where(p =>
                p.Key is string key /* && key != "SomeInternalProp" */
            ) // Filter if needed
            .ToList();

        if (propsToRender == null || !propsToRender.Any())
            return;

        writer.Write(" "); // Separator
        theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "{");
        var firstProp = true;
        foreach (var prop in propsToRender)
        {
            if (!firstProp)
                theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ", ");
            firstProp = false;

            theme.ApplyStyle(writer, RichNLogThemeStyle.Name, prop.Key.ToString() ?? "null");
            theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ": "); // Use colon-space

            // Render property value, assuming complex types should be serialized
            RenderPropertyValue(writer, prop.Value, theme, null, CaptureType.Serialize);
        }

        theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "}");
    }

    // --- Render Individual Property Value (Recursive) ---
    private void RenderPropertyValue(
        TextWriter writer,
        object? value,
        RichNLogTheme theme,
        string? format = null,
        CaptureType captureType = CaptureType.Normal,
        int maxDepth = 5,
        int currentDepth = 0
    )
    {
        // --- Handle Base Cases and Known Scalars ---
        // Accept intrinsic types when 1. not in serialize mode or 2. if we're in a nested context
        var renderIntrinsic = captureType != CaptureType.Serialize || currentDepth > 0;

        switch (value)
        {
            case null:
                theme.ApplyStyle(writer, RichNLogThemeStyle.Null, "null");
                return;
            case string s when renderIntrinsic:
                theme.ApplyStyle(writer, RichNLogThemeStyle.String, $"\"{EscapeString(s)}\""); // Add escaping
                return;
            case bool b when renderIntrinsic:
                theme.ApplyStyle(
                    writer,
                    RichNLogThemeStyle.Boolean,
                    (format?.ToLowerInvariant() == "u") ? (b ? "TRUE" : "FALSE") : (b ? "true" : "false")
                );
                return;
            // Handle numeric types applying format
            case (
                int
                or uint
                or long
                or ulong
                or decimal
                or byte
                or sbyte
                or short
                or ushort
                or float
                or double
            )
            and IFormattable formattableNum when renderIntrinsic:
                string numStr;
                try
                {
                    numStr = formattableNum.ToString(
                        format,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                catch (FormatException)
                {
                    numStr =
                        formattableNum.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                        ?? "null";
                }

                theme.ApplyStyle(writer, RichNLogThemeStyle.Number, numStr);
                return;
            // Handle DateTime types applying format
            case DateTime dt when value is IFormattable formattableDt && renderIntrinsic:
                string dtStr;
                try
                {
                    dtStr = formattableDt.ToString(
                        string.IsNullOrEmpty(format) ? "O" : format,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                catch (FormatException)
                {
                    dtStr = formattableDt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                }

                theme.ApplyStyle(writer, RichNLogThemeStyle.Scalar, $"\"{EscapeString(dtStr)}\"");
                return;
            case DateTimeOffset dto when value is IFormattable formattableDto && renderIntrinsic:
                string dtoStr;
                try
                {
                    dtoStr = formattableDto.ToString(
                        string.IsNullOrEmpty(format) ? "O" : format,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                catch (FormatException)
                {
                    dtoStr = formattableDto.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                }

                theme.ApplyStyle(writer, RichNLogThemeStyle.Scalar, $"\"{EscapeString(dtoStr)}\"");
                return;
            // Add Guid, TimeSpan etc. if needed
        }

        // --- Handle Recursive Cases (Collections, Dictionaries, Objects) or Forced Serialization ---
        if (currentDepth >= maxDepth)
        {
            theme.ApplyStyle(writer, RichNLogThemeStyle.Invalid, "\"...\""); // Indicate recursion limit
            return;
        }

        // Check for Dictionary
        if (value is IDictionary dict)
        {
            theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "{ ");
            var firstItem = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!firstItem)
                    theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ", ");
                firstItem = false;
                // Render key as string (usually)
                RenderPropertyValue(
                    writer,
                    entry.Key?.ToString(),
                    theme,
                    null,
                    CaptureType.Normal,
                    maxDepth,
                    currentDepth + 1
                );
                theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ": ");
                // Render value recursively
                RenderPropertyValue(
                    writer,
                    entry.Value,
                    theme,
                    null,
                    captureType,
                    maxDepth,
                    currentDepth + 1
                );
            }

            theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, " }");
            return;
        }

        // Check for non-string IEnumerable (Collections/Arrays)
        if (value is IEnumerable collection and not string)
        {
            theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "[");
            var firstItem = true;
            foreach (var item in collection)
            {
                if (!firstItem)
                    theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ", ");
                firstItem = false;
                // Render item recursively
                RenderPropertyValue(writer, item, theme, null, captureType, maxDepth, currentDepth + 1);
            }

            theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "]");
            return;
        }

        // --- Stringify: Render as string with quotes ---
        if (captureType == CaptureType.Stringify)
        {
            // Use ToString() and render as string
            theme.ApplyStyle(
                writer,
                RichNLogThemeStyle.String,
                $"\"{EscapeString(value.ToString() ?? "")}\""
            );
            return;
        }

        // --- Normal: Render as scalar ---
        if (captureType == CaptureType.Normal)
        {
            // Use ToString() and render as scalar
            theme.ApplyStyle(writer, RichNLogThemeStyle.Scalar, value.ToString() ?? "null");
            return;
        }

        // If we got here, it's likely CaptureType.Serialize, OR it's a complex object we didn't handle above
        // Render as an object using public properties
        theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, "{ ");
        try
        {
            var properties = value
                ?.GetType()
                .GetProperties(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                )
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0) // Exclude indexers
                .ToList();
            var firstProp = true;
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    if (!firstProp)
                    {
                        theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ", ");
                    }

                    firstProp = false;
                    theme.ApplyStyle(writer, RichNLogThemeStyle.Name, prop.Name);
                    theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ": ");
                    var propValue = prop.GetValue(value);
                    RenderPropertyValue(
                        writer,
                        propValue,
                        theme,
                        null,
                        captureType,
                        maxDepth,
                        currentDepth + 1
                    );
                }
            }
        }
        catch (Exception ex)
        {
            // Handle reflection errors, cyclical references might cause stack overflow without depth check
            /*if (!firstProp)
                theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, ", ");*/

            theme.ApplyStyle(writer, RichNLogThemeStyle.Invalid, $"\"<Error: {ex.GetType().Name}>\"");
        }

        theme.ApplyStyle(writer, RichNLogThemeStyle.TertiaryText, " }");
    }

    private void RenderPropertyValue(TextWriter writer, object? value, string? format = null)
    {
        RichNLogThemeStyle style;
        string textValue;
        var quoted = false;

        switch (value)
        {
            case null:
                style = RichNLogThemeStyle.Null;
                textValue = "null";
                break;
            case string s:
                style = RichNLogThemeStyle.String;
                textValue = s;
                quoted = true;
                break;
            // Handle numeric types - Apply format if present
            case int
            or uint
            or long
            or ulong
            or decimal
            or byte
            or sbyte
            or short
            or ushort
            or float
            or double when value is IFormattable formattableValue:
                style = RichNLogThemeStyle.Number;
                try
                {
                    textValue = formattableValue.ToString(
                        format,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                catch (FormatException)
                {
                    textValue =
                        formattableValue.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                        ?? "null";
                } // Fallback on format error

                break;
            case bool b:
                style = RichNLogThemeStyle.Boolean;
                // Booleans usually don't have formats, but handle just in case
                textValue =
                    (format?.ToLowerInvariant() == "u") ? (b ? "TRUE" : "FALSE") : (b ? "true" : "false");
                break;
            // Handle DateTime types - Apply format if present
            case DateTime dt when value is IFormattable formattableDt:
                style = RichNLogThemeStyle.Scalar;
                try
                {
                    textValue = formattableDt.ToString(
                        string.IsNullOrEmpty(format) ? "O" : format,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                catch (FormatException)
                {
                    textValue = formattableDt.ToString(
                        "O",
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                } // Fallback

                quoted = true;
                break;
            case DateTimeOffset dto when value is IFormattable formattableDto:
                style = RichNLogThemeStyle.Scalar;
                try
                {
                    textValue = formattableDto.ToString(
                        string.IsNullOrEmpty(format) ? "O" : format,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }
                catch (FormatException)
                {
                    textValue = formattableDto.ToString(
                        "O",
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                } // Fallback

                quoted = true;
                break;
            // Add other specific types if needed (Guid, TimeSpan, etc.)
            default:
                style = RichNLogThemeStyle.Scalar;
                // Attempt to apply format if the object is IFormattable
                if (!string.IsNullOrEmpty(format) && value is IFormattable formattableObj)
                {
                    try
                    {
                        textValue = formattableObj.ToString(
                            format,
                            System.Globalization.CultureInfo.InvariantCulture
                        );
                    }
                    catch (FormatException)
                    {
                        textValue = value?.ToString() ?? "null";
                    } // Fallback
                }
                else
                {
                    textValue = value?.ToString() ?? "null";
                }

                quoted = true;
                break;
        }

        // Apply quoting if necessary (basic) - Needs escaping logic for real use
        if (quoted)
            textValue = $"\"{textValue.Replace("\"", "\\\"")}\"";

        ApplyStyle(writer, style, textValue);
    }

    // --- Helper for basic string escaping ---
    private string EscapeString(string? s)
    {
        if (s == null)
            return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\""); // Basic escaping
    }

    private void ApplyStyle(TextWriter writer, RichNLogThemeStyle style, string text)
    {
        var ansi = Theme.GetAnsiStyle(style);
        if (!string.IsNullOrEmpty(ansi))
            writer.Write(ansi);
        writer.Write(text);
        if (!string.IsNullOrEmpty(ansi))
            writer.Write(RichNLogTheme.AnsiReset);
    }
}
