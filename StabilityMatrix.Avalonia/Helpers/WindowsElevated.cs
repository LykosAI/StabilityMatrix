using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Helpers;

[SupportedOSPlatform("windows")]
public static class WindowsElevated
{
    /// <summary>
    /// Move a file from source to target using elevated privileges.
    /// </summary>
    public static async Task<int> MoveFiles(params (string sourcePath, string targetPath)[] moves)
    {
        // Combine into single command
        var args = string.Join(" & ", moves.Select(
            x => $"move \"{x.sourcePath}\" \"{x.targetPath}\""));
        
        using var process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = $"/c {args}";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.Verb = "runas";
        
        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);
        
        return process.ExitCode;
    }
}
