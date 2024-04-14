namespace StabilityMatrix.Core.Models;

public class DownloadPackageVersionOptions
{
    public string? BranchName { get; set; }
    public string? CommitHash { get; set; }
    public string? VersionTag { get; set; }
    public bool IsLatest { get; set; }
    public bool IsPrerelease { get; set; }

    public string GetReadableVersionString() =>
        !string.IsNullOrWhiteSpace(VersionTag) ? VersionTag : $"{BranchName}@{CommitHash?[..7]}";

    public string ReadableVersionString => GetReadableVersionString();
}
