using System.Diagnostics;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class FocusControlNet : Fooocus
{
    public FocusControlNet(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper) { }

    public override string Name => "Fooocus-ControlNet-SDXL";
    public override string DisplayName { get; set; } = "Fooocus-ControlNet";
    public override string Author => "fenneishi";
    public override string Blurb =>
        "Fooocus-ControlNet adds more control to the original Fooocus software.";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/fenneishi/Fooocus-ControlNet-SDXL/blob/main/LICENSE";
    public override string LaunchCommand => "launch.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/fenneishi/Fooocus-ControlNet-SDXL/raw/main/asset/canny/snip.png");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
}
