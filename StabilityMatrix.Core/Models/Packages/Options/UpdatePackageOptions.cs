namespace StabilityMatrix.Core.Models.Packages;

public class UpdatePackageOptions
{
    public DownloadPackageVersionOptions VersionOptions { get; init; } = new();

    public PythonPackageOptions PythonOptions { get; init; } = new();

    public InstallPackageOptions AsInstallOptions()
    {
        return new InstallPackageOptions { VersionOptions = VersionOptions, PythonOptions = PythonOptions };
    }
}
