using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class DankDiffusion : BaseGitPackage
{
    public DankDiffusion(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }

    public override string Name => "dank-diffusion";
    public override string DisplayName { get; set; } = "Dank Diffusion";
    public override string Author => "mohnjiles";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => 
        "https://github.com/LykosAI/StabilityMatrix/blob/main/LICENSE";
    public override string Blurb => "A dank interface for diffusion";
    public override string LaunchCommand => "test";
    
    public override IReadOnlyList<string> ExtraLaunchCommands => new[]
    {
        "test-config",
    };
    
    public override Uri PreviewImageUri { get; }

    public override Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        throw new NotImplementedException();
    }

    public override Task SetupModelFolders(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

    public override Task UpdateModelFolders(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

    public override List<LaunchOptionDefinition> LaunchOptions { get; }
    public override Task<string> GetLatestVersion()
    {
        throw new NotImplementedException();
    }

    public override Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true)
    {
        throw new NotImplementedException();
    }
}
