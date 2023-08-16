namespace StabilityMatrix.Core.Models;

public record PackageVersion
{
    public required string TagName { get; set; }
    public string? ReleaseNotesMarkdown { get; set; }
}
