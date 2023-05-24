using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace StabilityMatrix.Helper;

public static class ProcessRunner
{
    public static async Task<string> RunProcessAsync(string fileName, string arguments)
    {
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

    public static Process RunProcess(string fileName, string arguments)

    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        
        return process;
    }
}
