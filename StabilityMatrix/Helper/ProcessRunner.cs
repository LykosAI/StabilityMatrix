using System.Diagnostics;
using System.Threading.Tasks;

namespace StabilityMatrix.Helper;

public static class ProcessRunner
{
    public static async Task<string> GetProcessOutputAsync(string fileName, string arguments)
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

    public static Process StartProcess(string fileName, string arguments)

    {
        var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        
        return process;
    }
}
