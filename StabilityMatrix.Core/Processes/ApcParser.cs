using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Parse escaped messages from subprocess
/// The message standard:
///   - Message events are prefixed with char 'APC' (9F)
///   - Followed by '[SM;'
///   - Json dict string of 2 strings, 'type' and 'data'
///   - Ends with char 'ST' (9C)
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal static class ApcParser
{
    public const char ApcEscape = (char) 0x9F;
    public const string IdPrefix = "[SM;";
    public const char StEscape = (char) 0x9C;
    
    /// <summary>
    /// Attempts to extract an APC message from the given text
    /// </summary>
    /// <returns>ApcMessage struct</returns>
    public static bool TryParse(string text, out ApcMessage? message)
    {
        message = null;
        var startIndex = text.IndexOf(ApcEscape);
        if (startIndex == -1) return false;
        
        // Check the IdPrefix follows the ApcEscape
        var idIndex = text.IndexOf(IdPrefix, startIndex + 1, StringComparison.Ordinal);
        if (idIndex == -1) return false;
        
        // Get the end index (ST escape)
        var stIndex = text.IndexOf(StEscape, idIndex + IdPrefix.Length);
        if (stIndex == -1) return false;

        // Extract the json string (between idIndex and stIndex)
        var json = text.Substring(idIndex + IdPrefix.Length, stIndex - idIndex - IdPrefix.Length);
        
        try
        {
            message = JsonSerializer.Deserialize<ApcMessage>(json);
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to parse APC message: {e.Message}");
            return false;
        }
    }
}
