using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper.Factory;

[Singleton(typeof(IPackageFactory))]
public class PackageFactory : IPackageFactory
{
    private readonly IGithubApiCache githubApiCache;
    private readonly ISettingsManager settingsManager;
    private readonly IDownloadService downloadService;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IPyRunner pyRunner;

    /// <summary>
    /// Mapping of package.Name to package
    /// </summary>
    private readonly Dictionary<string, BasePackage> basePackages;

    public PackageFactory(
        IEnumerable<BasePackage> basePackages,
        IGithubApiCache githubApiCache,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper,
        IPyRunner pyRunner
    )
    {
        this.githubApiCache = githubApiCache;
        this.settingsManager = settingsManager;
        this.downloadService = downloadService;
        this.prerequisiteHelper = prerequisiteHelper;
        this.pyRunner = pyRunner;
        this.basePackages = basePackages.ToDictionary(x => x.Name);
    }

    public BasePackage GetNewBasePackage(InstalledPackage installedPackage)
    {
        return installedPackage.PackageName switch
        {
            "ComfyUI" => new ComfyUI(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "Fooocus" => new Fooocus(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "stable-diffusion-webui"
                => new A3WebUI(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "Fooocus-ControlNet-SDXL"
                => new FocusControlNet(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "Fooocus-MRE"
                => new FooocusMre(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "InvokeAI" => new InvokeAI(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "kohya_ss"
                => new KohyaSs(
                    githubApiCache,
                    settingsManager,
                    downloadService,
                    prerequisiteHelper,
                    pyRunner
                ),
            "OneTrainer"
                => new OneTrainer(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "RuinedFooocus"
                => new RuinedFooocus(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "stable-diffusion-webui-forge"
                => new SDWebForge(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "stable-diffusion-webui-directml"
                => new StableDiffusionDirectMl(
                    githubApiCache,
                    settingsManager,
                    downloadService,
                    prerequisiteHelper
                ),
            "stable-diffusion-webui-ux"
                => new StableDiffusionUx(
                    githubApiCache,
                    settingsManager,
                    downloadService,
                    prerequisiteHelper
                ),
            "StableSwarmUI"
                => new StableSwarm(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "automatic"
                => new VladAutomatic(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "voltaML-fast-stable-diffusion"
                => new VoltaML(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "sdfx" => new Sdfx(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "mashb1t-fooocus"
                => new Mashb1tFooocus(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "reforge" => new Reforge(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "FluxGym" => new FluxGym(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            "SimpleSDXL"
                => new SimpleSDXL(githubApiCache, settingsManager, downloadService, prerequisiteHelper),
            _ => throw new ArgumentOutOfRangeException(nameof(installedPackage))
        };
    }

    public IEnumerable<BasePackage> GetAllAvailablePackages()
    {
        return basePackages.Values.OrderBy(p => p.InstallerSortOrder).ThenBy(p => p.DisplayName);
    }

    public BasePackage? FindPackageByName(string? packageName)
    {
        return packageName == null ? null : basePackages.GetValueOrDefault(packageName);
    }

    public BasePackage? this[string packageName] => basePackages[packageName];

    /// <inheritdoc />
    public PackagePair? GetPackagePair(InstalledPackage? installedPackage)
    {
        if (installedPackage?.PackageName is not { } packageName)
            return null;

        return !basePackages.TryGetValue(packageName, out var basePackage)
            ? null
            : new PackagePair(installedPackage, basePackage);
    }

    public IEnumerable<BasePackage> GetPackagesByType(PackageType packageType) =>
        basePackages.Values.Where(p => p.PackageType == packageType);
}
