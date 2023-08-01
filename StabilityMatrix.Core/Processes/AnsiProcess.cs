using System.Diagnostics;
using System.Text;

namespace StabilityMatrix.Core.Processes;

/// <summary>
/// Process supporting parsing of ANSI escape sequences
/// </summary>
public class AnsiProcess : Process
{
    private AsyncStreamReader? stdoutReader;
    private AsyncStreamReader? stderrReader;
    
    public AnsiProcess(ProcessStartInfo startInfo)
    {
        StartInfo = startInfo;
        EnableRaisingEvents = false;
        
        StartInfo.UseShellExecute = false;
        StartInfo.CreateNoWindow = true;
        StartInfo.RedirectStandardOutput = true;
        StartInfo.RedirectStandardInput = true;
        StartInfo.RedirectStandardError = true;
        
        // Need this to parse ANSI escape sequences correctly
        StartInfo.StandardOutputEncoding = Encoding.UTF8;
        StartInfo.StandardErrorEncoding = Encoding.UTF8;
        StartInfo.StandardInputEncoding = Encoding.UTF8;
    }

    /// <summary>
    /// Start asynchronous reading of stdout and stderr
    /// </summary>
    /// <param name="callback">Called on each new line</param>
    public void BeginAnsiRead(Action<ProcessOutput> callback)
    {
        var stdoutStream = StandardOutput.BaseStream;
        stdoutReader = new AsyncStreamReader(stdoutStream, s =>
        {
            if (s == null) return;
            callback(ProcessOutput.FromStdOutLine(s));
        }, StandardOutput.CurrentEncoding);
        
        var stderrStream = StandardError.BaseStream;
        stderrReader = new AsyncStreamReader(stderrStream, s =>
        {
            if (s == null) return;
            callback(ProcessOutput.FromStdErrLine(s));
        }, StandardError.CurrentEncoding);

        stdoutReader.BeginReadLine();
        stderrReader.BeginReadLine();
    }
    
    /// <summary>
    /// Waits for output readers to finish
    /// </summary>
    public async Task WaitUntilOutputEOF(CancellationToken ct = default)
    {
        if (stdoutReader is not null)
        {
            await stdoutReader.EOF.WaitAsync(ct).ConfigureAwait(false);
        }
        if (stderrReader is not null)
        {
            await stderrReader.EOF.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Cancels stream readers, no effect if already cancelled
    /// </summary>
    public void CancelStreamReaders()
    {
        stdoutReader?.CancelOperation();
        stderrReader?.CancelOperation();
    }

    protected override void Dispose(bool disposing)
    {
        CancelStreamReaders();
        stdoutReader?.Dispose();
        stdoutReader = null;
        stderrReader?.Dispose();
        stderrReader = null;
        base.Dispose(disposing);
    }
}
