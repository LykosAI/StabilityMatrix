using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, FocusControlNet>(Duplicate = DuplicateStrategy.Append)]
public class FocusControlNet(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : Fooocus(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "Fooocus-ControlNet-SDXL";
    public override string DisplayName { get; set; } = "Fooocus-ControlNet";
    public override string Author => "fenneishi";
    public override string Blurb => "Fooocus-ControlNet adds more control to the original Fooocus software.";
    public override string Disclaimer => "This package may no longer be actively maintained";
    public override string LicenseUrl =>
        "https://github.com/fenneishi/Fooocus-ControlNet-SDXL/blob/main/LICENSE";
    public override Uri PreviewImageUri =>
        new("https://github.com/fenneishi/Fooocus-ControlNet-SDXL/raw/main/asset/canny/snip.png");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Impossible;
    public override bool OfferInOneClickInstaller => false;

    public override SharedFolderLayout SharedFolderLayout =>
        base.SharedFolderLayout with
        {
            RelativeConfigPath = "user_path_config.txt"
        };
}
