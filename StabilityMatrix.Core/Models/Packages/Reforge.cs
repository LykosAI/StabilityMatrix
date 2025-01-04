using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Reforge>(Duplicate = DuplicateStrategy.Append)]
public class Reforge(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : SDWebForge(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "reforge";
    public override string Author => "Panchovix";
    public override string RepositoryName => "stable-diffusion-webui-reForge";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI reForge";
    public override string Blurb =>
        "Stable Diffusion WebUI reForge is a platform on top of Stable Diffusion WebUI (based on Gradio) to make development easier, optimize resource management, speed up inference, and study experimental features.";
    public override string LicenseUrl =>
        "https://github.com/Panchovix/stable-diffusion-webui-reForge/blob/main/LICENSE.txt";
    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/reforge/preview.webp");

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.ReallyRecommended;
}
