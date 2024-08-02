namespace StabilityMatrix.Core.Models;

/// <summary>
/// Defines a remote downloadable resource.
/// </summary>
public readonly record struct RemoteResource
{
    public required Uri Url { get; init; }

    public Uri[]? FallbackUrls { get; init; }

    public string? FileNameOverride { get; init; }

    public string FileName => FileNameOverride ?? Path.GetFileName(Url.ToString());

    public string? HashSha256 { get; init; }

    /// <summary>
    /// Type info, for remote models this is <see cref="SharedFolderType"/> of the model.
    /// </summary>
    public object? ContextType { get; init; }

    public Uri? InfoUrl { get; init; }

    public string? LicenseType { get; init; }

    public Uri? LicenseUrl { get; init; }

    public string? Author { get; init; }

    /// <summary>
    /// Whether to auto-extract the archive after download
    /// </summary>
    public bool AutoExtractArchive { get; init; }

    /// <summary>
    /// Optional relative path to extract the archive to, if AutoExtractArchive is true
    /// </summary>
    public string? ExtractRelativePath { get; init; }

    public string? RelativePath { get; init; }
}
