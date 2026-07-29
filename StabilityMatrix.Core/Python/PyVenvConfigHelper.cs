using System.Text;
using NLog;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Helper for reading and writing pyvenv.cfg files.
/// pyvenv.cfg is a simple key = value format without INI sections,
/// so we manipulate it directly instead of using a section-based INI parser.
/// </summary>
public static class PyVenvConfigHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Write or update the path keys in a pyvenv.cfg file.
    /// Sets home, base-prefix, base-exec-prefix to <paramref name="pythonDirectory"/>
    /// and base-executable to <paramref name="baseExecutable"/>.
    /// Other existing keys are preserved in their original order.
    /// </summary>
    public static void WritePyVenvCfg(string cfgPath, string pythonDirectory, string baseExecutable)
    {
        var lines = File.ReadAllLines(cfgPath);
        var sb = new StringBuilder();
        var hasHome = false;
        var hasBasePrefix = false;
        var hasBaseExecPrefix = false;
        var hasBaseExecutable = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("home", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
            {
                sb.AppendLine($"home = {pythonDirectory}");
                hasHome = true;
            }
            else if (
                trimmed.StartsWith("base-prefix", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('=')
            )
            {
                sb.AppendLine($"base-prefix = {pythonDirectory}");
                hasBasePrefix = true;
            }
            else if (
                trimmed.StartsWith("base-exec-prefix", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains('=')
            )
            {
                sb.AppendLine($"base-exec-prefix = {pythonDirectory}");
                hasBaseExecPrefix = true;
            }
            else if (
                trimmed.StartsWith("base-executable", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains('=')
            )
            {
                sb.AppendLine($"base-executable = {baseExecutable}");
                hasBaseExecutable = true;
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        // Append any missing keys
        if (!hasHome)
        {
            sb.AppendLine($"home = {pythonDirectory}");
        }
        if (!hasBasePrefix)
        {
            sb.AppendLine($"base-prefix = {pythonDirectory}");
        }
        if (!hasBaseExecPrefix)
        {
            sb.AppendLine($"base-exec-prefix = {pythonDirectory}");
        }
        if (!hasBaseExecutable)
        {
            sb.AppendLine($"base-executable = {baseExecutable}");
        }

        File.WriteAllText(cfgPath, sb.ToString());

        Logger.Debug(
            "Wrote pyvenv.cfg: home={PyDir}, base-executable={PyExe}",
            pythonDirectory,
            baseExecutable
        );
    }
}
