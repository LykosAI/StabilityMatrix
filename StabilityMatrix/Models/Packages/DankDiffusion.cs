using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Models.Packages;

public class DankDiffusion : BasePackage
{
    public override string Name => "dank-diffusion";
    public override string DisplayName { get; set; } = "dank-diffusion";
    public override string Author => "mohnjiles";
    public override string GithubUrl => "https://github.com/mohnjiles/dank-diffusion";
    public override string LaunchCommand => "";

    public override Task<IEnumerable<GithubRelease>> GetReleaseTags()
    {
        throw new System.NotImplementedException();
    }

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

    public override Task<string> GetLatestVersion()
    {
        throw new System.NotImplementedException();
    }

    public override Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true)
    {
        throw new System.NotImplementedException();
    }

    public override Task<IEnumerable<GithubCommit>> GetAllCommits(string branch, int page = 1, int perPage = 10)
    {
        throw new System.NotImplementedException();
    }

    public override string DownloadLocation { get; }
    public override string InstallLocation { get; set; }
    public override Task<IEnumerable<GithubBranch>> GetAllBranches()
    {
        throw new System.NotImplementedException();
    }

    public override Task<string?> DownloadPackage(string version, bool isUpdate = false)
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

    public override Task<bool> CheckForUpdates(string installedPackageName)
    {
        throw new System.NotImplementedException();
    }

    public override Task<string?> Update()
    {
        throw new System.NotImplementedException();
    }

    public override string DefaultLaunchArguments => "";
}
