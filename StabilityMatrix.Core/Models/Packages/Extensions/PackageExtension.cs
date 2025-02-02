namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record PackageExtension
{
    public required string Author { get; init; }

    public required string Title { get; init; }

    public required Uri Reference { get; init; }

    public required IEnumerable<Uri> Files { get; init; }

    public IEnumerable<string>? Pip { get; init; }

    public string? Description { get; init; }

    public string? InstallType { get; init; }

    public bool IsInstalled { get; init; }
}
