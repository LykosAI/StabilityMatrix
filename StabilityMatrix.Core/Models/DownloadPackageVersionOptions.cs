namespace StabilityMatrix.Core.Models;

public class DownloadPackageVersionOptions
{
    public string? BranchName { get; set; }
    public string? CommitHash { get; set; }
    public string? VersionTag { get; set; }
    public bool IsLatest { get; set; }
}
