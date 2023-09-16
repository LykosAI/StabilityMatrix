using System.Globalization;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Settings;

[JsonSerializable(typeof(Settings))]
public class Settings
{
    public int? Version { get; set; } = 1;
    public bool FirstLaunchSetupComplete { get; set; }
    public string? Theme { get; set; } = "Dark";
    public string? Language { get; set; } = GetDefaultCulture().Name;

    public List<InstalledPackage> InstalledPackages { get; set; } = new();

    [JsonPropertyName("ActiveInstalledPackage")]
    public Guid? ActiveInstalledPackageId { get; set; }

    /// <summary>
    /// The first installed package matching the <see cref="ActiveInstalledPackageId"/>
    /// or null if no matching package
    /// </summary>
    [JsonIgnore]
    public InstalledPackage? ActiveInstalledPackage
    {
        get =>
            ActiveInstalledPackageId == null
                ? null
                : InstalledPackages.FirstOrDefault(x => x.Id == ActiveInstalledPackageId);
        set => ActiveInstalledPackageId = value?.Id;
    }

    public bool HasSeenWelcomeNotification { get; set; }
    public List<string>? PathExtensions { get; set; }
    public string? WebApiHost { get; set; }
    public string? WebApiPort { get; set; }

    // UI states
    public bool ModelBrowserNsfwEnabled { get; set; }
    public bool IsNavExpanded { get; set; }
    public bool IsImportAsConnected { get; set; }
    public bool ShowConnectedModelImages { get; set; }
    public SharedFolderType? SharedFolderVisibleCategories { get; set; } =
        SharedFolderType.StableDiffusion | SharedFolderType.Lora | SharedFolderType.LyCORIS;

    public WindowSettings? WindowSettings { get; set; }

    public ModelSearchOptions? ModelSearchOptions { get; set; }

    /// <summary>
    /// Whether prompt auto completion is enabled
    /// </summary>
    public bool IsPromptCompletionEnabled { get; set; }

    /// <summary>
    /// Relative path to the tag completion CSV file from 'LibraryDir/Tags'
    /// </summary>
    public string? TagCompletionCsv { get; set; }

    /// <summary>
    /// Whether to remove underscores from completions
    /// </summary>
    public bool IsCompletionRemoveUnderscoresEnabled { get; set; }

    /// <summary>
    /// Whether the Inference Image Viewer shows pixel grids at high zoom levels
    /// </summary>
    public bool IsImageViewerPixelGridEnabled { get; set; } = true;
    public bool RemoveFolderLinksOnShutdown { get; set; }

    public bool IsDiscordRichPresenceEnabled { get; set; }

    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    public HashSet<string>? InstalledModelHashes { get; set; } = new();

    public float AnimationScale { get; set; } = 1.0f;

    public bool AutoScrollLaunchConsoleToEnd { get; set; } = true;

    public void RemoveInstalledPackageAndUpdateActive(InstalledPackage package)
    {
        RemoveInstalledPackageAndUpdateActive(package.Id);
    }

    public void RemoveInstalledPackageAndUpdateActive(Guid id)
    {
        InstalledPackages.RemoveAll(x => x.Id == id);
        UpdateActiveInstalledPackage();
    }

    /// <summary>
    /// Update ActiveInstalledPackage if not valid
    /// uses first package or null if no packages
    /// </summary>
    public void UpdateActiveInstalledPackage()
    {
        // Empty packages - set to null
        if (InstalledPackages.Count == 0)
        {
            ActiveInstalledPackageId = null;
        }
        // Active package is not in package - set to first package
        else if (InstalledPackages.All(x => x.Id != ActiveInstalledPackageId))
        {
            ActiveInstalledPackageId = InstalledPackages[0].Id;
        }
    }

    /// <summary>
    /// Return either the system default culture, if supported, or en-US
    /// </summary>
    /// <returns></returns>
    public static CultureInfo GetDefaultCulture()
    {
        var supportedCultures = new[] { "en-US", "ja-JP", "zh-Hans", "zh-Hant" };

        var systemCulture = CultureInfo.InstalledUICulture;

        if (systemCulture.Name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            return new CultureInfo("zh-Hans");
        }

        if (systemCulture.Name.StartsWith("zh-Hant"))
        {
            return new CultureInfo("zh-Hant");
        }

        return supportedCultures.Contains(systemCulture.Name)
            ? systemCulture
            : new CultureInfo("en-US");
    }
}
