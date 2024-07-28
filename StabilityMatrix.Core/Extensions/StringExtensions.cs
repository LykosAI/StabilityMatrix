using System.Diagnostics.Contracts;
using System.Text;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Extensions;

public static class StringExtensions
{
    private static string EncodeNonAsciiCharacters(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            // If not ascii or not printable
            if (c > 127 || c < 32)
            {
                // This character is too big for ASCII
                var encodedValue = "\\u" + ((int)c).ToString("x4");
                sb.Append(encodedValue);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts string to repr
    /// </summary>
    public static string ToRepr(this string? str)
    {
        if (str is null)
        {
            return "<null>";
        }
        using var writer = new StringWriter();
        writer.Write("'");
        foreach (var ch in str)
        {
            writer.Write(
                ch switch
                {
                    '\0' => "\\0",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    // Non ascii
                    _ when ch > 127 || ch < 32 => $"\\u{(int)ch:x4}",
                    _ => ch.ToString()
                }
            );
        }
        writer.Write("'");

        return writer.ToString();
    }

    /// <summary>
    /// Counts continuous sequence of a character
    /// from the start of the string
    /// </summary>
    public static int CountStart(this string str, char c)
    {
        var count = 0;
        foreach (var ch in str)
        {
            if (ch == c)
            {
                count++;
            }
            else
            {
                break;
            }
        }
        return count;
    }

    /// <summary>
    /// Strips the substring from the start of the string
    /// </summary>
    [Pure]
    public static string StripStart(this string str, string subString)
    {
        var index = str.IndexOf(subString, StringComparison.Ordinal);
        return index < 0 ? str : str.Remove(index, subString.Length);
    }

    /// <summary>
    /// Strips the substring from the end of the string
    /// </summary>
    [Pure]
    public static string StripEnd(this string str, string subString)
    {
        var index = str.LastIndexOf(subString, StringComparison.Ordinal);
        return index < 0 ? str : str.Remove(index, subString.Length);
    }

    /// <summary>
    /// Splits lines by \n and \r\n
    /// </summary>
    [Pure]
    // ReSharper disable once ReturnTypeCanBeEnumerable.Global
    public static string[] SplitLines(this string str, StringSplitOptions options = StringSplitOptions.None)
    {
        return str.Split(new[] { "\r\n", "\n" }, options);
    }

    /// <summary>
    /// Normalizes directory separator characters in a given path
    /// </summary>
    [Pure]
    public static string NormalizePathSeparators(this string path)
    {
        return path.Replace('\\', '/');
    }
}
