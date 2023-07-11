namespace StabilityMatrix.Core.Models;

[Flags]
public enum PackageVersionType
{
    None = 0,
    GithubRelease = 1 << 0,
    Commit = 1 << 1
}
