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
        var args = string.Join(" & ", moves.Select(x => $"move \"{x.sourcePath}\" \"{x.targetPath}\""));

        using var process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = $"/c {args}";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.Verb = "runas";

        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);

        return process.ExitCode;
    }

    /// <summary>
    /// Set a registry key integer using elevated privileges.
    /// </summary>
    public static async Task<int> SetRegistryValue(string key, string valueName, int value)
    {
        using var process = new Process();
        process.StartInfo.FileName = "reg.exe";
        process.StartInfo.Arguments = $"add \"{key}\" /v \"{valueName}\" /t REG_DWORD /d {value} /f";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.Verb = "runas";

        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);

        return process.ExitCode;
    }

    /// <summary>
    /// Set a registry key string using elevated privileges.
    /// </summary>
    public static async Task<int> SetRegistryValue(string key, string valueName, string value)
    {
        using var process = new Process();
        process.StartInfo.FileName = "reg.exe";
        process.StartInfo.Arguments = $"add \"{key}\" /v \"{valueName}\" /t REG_SZ /d \"{value}\" /f";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.Verb = "runas";

        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);

        return process.ExitCode;
    }
}
