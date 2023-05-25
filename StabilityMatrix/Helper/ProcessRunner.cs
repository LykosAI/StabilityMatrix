using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;

namespace StabilityMatrix.Helper;

public static class ProcessRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public static async Task<string> GetProcessOutputAsync(string fileName, string arguments)
    {
        Logger.Trace($"Starting process '{fileName}' with arguments '{arguments}'");
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    public static Process StartProcess(string fileName, string arguments, Action<string?>? outputDataReceived = null)
    {
        Logger.Trace($"Starting process '{fileName}' with arguments '{arguments}'");
        var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;

        if (outputDataReceived != null)
        {
            process.OutputDataReceived += (_, args) => outputDataReceived(args.Data);
        }

        process.Start();

        if (outputDataReceived != null)
        {
            process.BeginOutputReadLine();
        }

        return process;
    }
}
