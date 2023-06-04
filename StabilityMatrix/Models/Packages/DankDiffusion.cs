using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Services;

namespace StabilityMatrix.Models.Packages;

public class DankDiffusion : BaseGitPackage
{
    public DankDiffusion(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService) :
        base(githubApi, settingsManager, downloadService)
    {
    }

    public override string Name => "dank-diffusion";
    public override string DisplayName { get; set; } = "Dank Diffusion";
    public override string Author => "mohnjiles";
    public override string LaunchCommand { get; }
    public override Uri PreviewImageUri { get; }

    public override Task RunPackage(string installedPackagePath, string arguments)
    {
        throw new System.NotImplementedException();
    }

    public override List<LaunchOptionDefinition> LaunchOptions { get; }
    public override Task<string> GetLatestVersion()
    {
        throw new System.NotImplementedException();
    }

    public override Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true)
    {
        throw new System.NotImplementedException();
    }
}
