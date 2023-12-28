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
            .Select(line => line.Split(':', 2))
            .Where(split => split.Length == 2)
            .Select(split => new KeyValuePair<string, string>(split[0].Trim(), split[1].Trim()))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return new PipShowResult
        {
            Name = lines["Name"],
            Version = lines["Version"],
            Summary = lines.GetValueOrDefault("Summary"),
            HomePage = lines.GetValueOrDefault("Home-page"),
            Author = lines.GetValueOrDefault("Author"),
            AuthorEmail = lines.GetValueOrDefault("Author-email"),
            License = lines.GetValueOrDefault("License"),
            Location = lines.GetValueOrDefault("Location"),
            Requires = lines
                .GetValueOrDefault("Requires")
                ?.Split(',', StringSplitOptions.TrimEntries)
                .ToList(),
            RequiredBy = lines
                .GetValueOrDefault("Required-by")
                ?.Split(',', StringSplitOptions.TrimEntries)
                .ToList()
        };
    }
}
