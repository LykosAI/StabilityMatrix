using System.Diagnostics;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Process supporting parsing of ANSI escape sequences
/// </summary>
public class AnsiProcess : Process
{
    public AnsiProcess(ProcessStartInfo startInfo)
    {
        StartInfo = startInfo;
        EnableRaisingEvents = false;
        
        StartInfo.UseShellExecute = false;
        StartInfo.CreateNoWindow = true;
        StartInfo.RedirectStandardOutput = true;
        StartInfo.RedirectStandardInput = true;
        StartInfo.RedirectStandardError = true;
    }

    /// <summary>
    /// Start asynchronous reading of stdout and stderr
    /// </summary>
    /// <param name="callback">Called on each new line</param>
    public void BeginAnsiRead(Action<ProcessOutput> callback)
    {
        var stdoutStream = StandardOutput.BaseStream;
        var stdoutReader = new AsyncStreamReader(stdoutStream, s =>
        {
            if (s == null) return;
            callback(ProcessOutput.FromStdOutLine(s));
        }, StandardOutput.CurrentEncoding);
        
        var stderrStream = StandardError.BaseStream;
        var stderrReader = new AsyncStreamReader(stderrStream, s =>
        {
            if (s == null) return;
            callback(ProcessOutput.FromStdErrLine(s));
        }, StandardError.CurrentEncoding);

        stdoutReader.BeginReadLine();
        stderrReader.BeginReadLine();
    }
}
