using System.Diagnostics;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Python;

public record PipShowResult
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public string? Summary { get; init; }

    public string? HomePage { get; init; }

    public string? Author { get; init; }

    public string? AuthorEmail { get; init; }

    public string? License { get; init; }

    public string? Location { get; init; }

    public List<string>? Requires { get; init; }

    public List<string>? RequiredBy { get; init; }

    public static PipShowResult Parse(string output)
    {
        // Decode each line by splitting on first ":" to key and value
        var lines = output
            .SplitLines(StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            // Filter warning lines
            .Where(line => !line.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var indexOfLicense = GetIndexBySubstring(lines, "License:");
        var indexOfLocation = GetIndexBySubstring(lines, "Location:");

        var licenseText =
            indexOfLicense == -1 ? null : string.Join('\n', lines[indexOfLicense..indexOfLocation]);

        if (indexOfLicense != -1)
        {
            lines.RemoveRange(indexOfLicense, indexOfLocation - indexOfLicense);
        }

        var linesDict = new Dictionary<string, string>();
        foreach (var line in lines)
        {
            var split = line.Split(':', 2);
            if (split.Length != 2)
                continue;

            var key = split[0].Trim();
            var value = split[1].Trim();

            if (key == "Name" && linesDict.ContainsKey("Name"))
            {
                // We've hit a new package, so stop parsing
                break;
            }

            linesDict.TryAdd(key, value);
        }

        return new PipShowResult
        {
            Name = linesDict["Name"],
            Version = linesDict["Version"],
            Summary = linesDict.GetValueOrDefault("Summary"),
            HomePage = linesDict.GetValueOrDefault("Home-page"),
            Author = linesDict.GetValueOrDefault("Author"),
            AuthorEmail = linesDict.GetValueOrDefault("Author-email"),
            License = licenseText,
            Location = linesDict.GetValueOrDefault("Location"),
            Requires = linesDict
                .GetValueOrDefault("Requires")
                ?.Split(',', StringSplitOptions.TrimEntries)
                .ToList(),
            RequiredBy = linesDict
                .GetValueOrDefault("Required-by")
                ?.Split(',', StringSplitOptions.TrimEntries)
                .ToList(),
        };
    }

    private static int GetIndexBySubstring(List<string> lines, string searchString)
    {
        var index = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith(searchString, StringComparison.OrdinalIgnoreCase))
                continue;

            index = i;
            break;
        }

        return index;
    }
}
