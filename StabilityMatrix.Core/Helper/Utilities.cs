using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Helper;

public static partial class Utilities
{
    public static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version == null
            ? "(Unknown)"
            : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public static void CopyDirectory(
        string sourceDir,
        string destinationDir,
        bool recursive,
        bool includeReparsePoints = false
    )
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        var dirs = includeReparsePoints
            ? dir.GetDirectories()
            : dir.GetDirectories().Where(d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint));

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            if (file.FullName == targetFilePath)
                continue;
            file.CopyTo(targetFilePath, true);
        }

        if (!recursive)
            return;

        // If recursive and copying subdirectories, recursively call this method
        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, true);
        }
    }

    public static MemoryStream? GetMemoryStreamFromFile(string filePath)
    {
        var fileBytes = File.ReadAllBytes(filePath);
        var stream = new MemoryStream(fileBytes);
        stream.Position = 0;

        return stream;
    }

    public static async Task<string> WhichAsync(string arg)
    {
        using var process = new Process();
        process.StartInfo.FileName = Compat.IsWindows ? "where.exe" : "which";
        process.StartInfo.Arguments = arg;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;

        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);

        return await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
    }

    public static int GetNumDaysTilBeginningOfNextMonth()
    {
        var now = DateTimeOffset.UtcNow;
        var firstDayOfNextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var daysUntilNextMonth = (firstDayOfNextMonth - now).Days;
        return daysUntilNextMonth;
    }

    public static string RemoveHtml(string? stringWithHtml)
    {
        var pruned =
            stringWithHtml
                ?.Replace("<br/>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("<br />", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</p>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</h1>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</h2>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</h3>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</h4>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</h5>", $"{Environment.NewLine}{Environment.NewLine}")
                .Replace("</h6>", $"{Environment.NewLine}{Environment.NewLine}") ?? string.Empty;
        pruned = HtmlRegex().Replace(pruned, string.Empty);
        return pruned;
    }

    /// <summary>
    /// Try to find pyvenv.cfg in common locations and parse its version into PyVersion.
    /// </summary>
    public static bool TryGetPyVenvVersion(string packageLocation, out PyVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(packageLocation))
            return false;

        // Common placements
        var candidates = new[]
        {
            Path.Combine(packageLocation, "pyvenv.cfg"),
            Path.Combine(packageLocation, "venv", "pyvenv.cfg"),
            Path.Combine(packageLocation, ".venv", "pyvenv.cfg"),
        };

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            if (!TryReadVersionFromCfg(candidate, out var pyv))
                continue;

            version = pyv;
            return true;
        }

        return false;
    }

    private static bool TryReadVersionFromCfg(string cfgFile, out PyVersion version)
    {
        version = default;

        var kv = ReadKeyValues(cfgFile);

        // Prefer version_info (e.g., "3.10.11.final.0"), fall back to version (e.g., "3.12.0")
        if (!kv.TryGetValue("version_info", out var raw) || string.IsNullOrWhiteSpace(raw))
            kv.TryGetValue("version", out raw);

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // Grab the first 1–3 dot-separated integers and ignore anything after (e.g., ".final.0", ".rc1")
        // Examples matched: "3", "3.12", "3.10.11" (from "3.10.11.final.0")
        var m = Regex.Match(raw, @"(?<!\d)(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (!m.Success)
            return false;

        var major = int.Parse(m.Groups[1].Value);
        var minor = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        var micro = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;

        version = new PyVersion(major, minor, micro);
        return true;
    }

    private static Dictionary<string, string> ReadKeyValues(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                continue;

            var idx = trimmed.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = trimmed[..idx].Trim();
            var val = trimmed[(idx + 1)..].Trim();
            dict[key] = val;
        }
        return dict;
    }

    /// <summary>
    /// Returns the simplified aspect ratio as a tuple: (widthRatio, heightRatio).
    /// e.g. GetAspectRatio(1920,1080) -> (16,9)
    /// </summary>
    public static (int widthRatio, int heightRatio) GetAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Width and height must be positive.");

        var gcd = Gcd(width, height);
        return (width / gcd, height / gcd);
    }

    // Euclidean GCD
    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var rem = a % b;
            a = b;
            b = rem;
        }
        return a;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlRegex();
}
