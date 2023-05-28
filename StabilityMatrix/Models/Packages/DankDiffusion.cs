using System.Threading.Tasks;

namespace StabilityMatrix.Models.Packages;

public class DankDiffusion : BasePackage
{
    public override string Name => "dank-diffusion";
    public override string DisplayName => "Dank Diffusion";
    public override string Author => "mohnjiles";
    public override string GithubUrl => "https://github.com/mohnjiles/dank-diffusion";
    public override string LaunchCommand => "";

    public override Task<string?> DownloadPackage(bool isUpdate = false)
    {
        throw new System.NotImplementedException();
    }

    public override Task InstallPackage(bool isUpdate = false)
    {
        throw new System.NotImplementedException();
    }

    public override Task RunPackage(string installedPackagePath, string arguments)
    {
        throw new System.NotImplementedException();
    }

    public override Task Shutdown()
    {
        throw new System.NotImplementedException();
    }

    public override Task<bool> CheckForUpdates()
    {
        throw new System.NotImplementedException();
    }

    public override Task<string?> Update()
    {
        throw new System.NotImplementedException();
    }

    public override string DefaultLaunchArguments => "";
}
