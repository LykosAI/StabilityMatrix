namespace StabilityMatrix.Core.Models.Packages;

public class InstallPackageOptions
{
    public DownloadPackageVersionOptions VersionOptions { get; init; } = new();

    public PythonPackageOptions PythonOptions { get; init; } = new();

    public SharedFolderMethod SharedFolderMethod { get; init; } = SharedFolderMethod.None;

    public bool IsUpdate { get; init; }
}
