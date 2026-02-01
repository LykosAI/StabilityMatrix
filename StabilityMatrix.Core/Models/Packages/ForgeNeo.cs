using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, ForgeNeo>(Duplicate = DuplicateStrategy.Append)]
public class ForgeNeo(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : ForgeClassic(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "forge-neo";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI Forge - Neo";
    public override string MainBranch => "neo";
    public override PackageType PackageType => PackageType.SdInference;

    public override string Blurb =>
        "Neo mainly serves as an continuation for the \"latest\" version of Forge. Additionally, this fork is focused on optimization and usability, with the main goal of being the lightest WebUI without any bloatwares.";
}
