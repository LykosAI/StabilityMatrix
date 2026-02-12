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
    IPyInstallationManager pyInstallationManager
) : ForgeClassic(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "forge-neo";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI Forge - Neo";
    public override string MainBranch => "neo";
    public override PackageType PackageType => PackageType.SdInference;

    /// <summary>
    /// Forge Neo requires Python 3.12+ due to dependencies like audioop-lts.
    /// See: https://github.com/LykosAI/StabilityMatrix/issues/1138
    /// </summary>
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;
}
