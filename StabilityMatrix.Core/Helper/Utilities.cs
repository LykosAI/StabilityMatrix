using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

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

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlRegex();
}
