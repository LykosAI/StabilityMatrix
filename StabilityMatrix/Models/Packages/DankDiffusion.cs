using System.Collections.Generic;
using System.Threading.Tasks;

namespace StabilityMatrix.Models.Packages;

public class DankDiffusion : BasePackage
{
    public override string Name => "dank-diffusion";
    public override string DisplayName => "Dank Diffusion";
    public override string Author => "mohnjiles";
    public override string GithubUrl => "https://github.com/mohnjiles/dank-diffusion";
    public override string LaunchCommand => "";

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new LaunchOptionDefinition
        {
            Name = "API",
            Options = new List<string> { "--api" }
        },
        new LaunchOptionDefinition
        {
            Name = "VRAM",
            Options = new List<string> { "--lowvram", "--medvram" }
        },
        new LaunchOptionDefinition
        {
            Name = "Xformers",
            Options = new List<string> { "--xformers" }
        }
    };
    
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
