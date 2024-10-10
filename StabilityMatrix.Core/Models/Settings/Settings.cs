using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Semver;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Core.Models.Settings;

public class Settings
{
    public int? Version { get; set; } = 1;
    public bool FirstLaunchSetupComplete { get; set; }
    public string? Theme { get; set; } = "Dark";
    public string? Language { get; set; } = GetDefaultCulture().Name;

    public NumberFormatMode NumberFormatMode { get; set; } = NumberFormatMode.CurrentCulture;

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

    [JsonPropertyName("PreferredWorkflowPackage")]
    public Guid? PreferredWorkflowPackageId { get; set; }

    [JsonIgnore]
    public InstalledPackage? PreferredWorkflowPackage
    {
        get =>
            PreferredWorkflowPackageId == null
                ? null
                : InstalledPackages.FirstOrDefault(x => x.Id == PreferredWorkflowPackageId);
        set => PreferredWorkflowPackageId = value?.Id;
    }

    public bool HasSeenWelcomeNotification { get; set; }
    public List<string>? PathExtensions { get; set; }
    public string? WebApiHost { get; set; }
    public string? WebApiPort { get; set; }

    /// <summary>
    /// Preferred update channel
    /// </summary>
    public UpdateChannel PreferredUpdateChannel { get; set; } = UpdateChannel.Stable;

    /// <summary>
    /// Whether to check for updates
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// The last auto-update version that had a notification dismissed by the user
    /// </summary>
    [JsonConverter(typeof(SemVersionJsonConverter))]
    public SemVersion? LastSeenUpdateVersion { get; set; }

    /// <summary>
    /// Set to the version the user is updating from when updating
    /// </summary>
    [JsonConverter(typeof(SemVersionJsonConverter))]
    public SemVersion? UpdatingFromVersion { get; set; }

    // UI states
    public bool ModelBrowserNsfwEnabled { get; set; }
    public bool IsNavExpanded { get; set; }
    public bool IsImportAsConnected { get; set; }
    public bool ShowConnectedModelImages { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<SharedFolderType>))]
    public SharedFolderType SharedFolderVisibleCategories { get; set; } =
        SharedFolderType.StableDiffusion | SharedFolderType.Lora | SharedFolderType.LyCORIS;

    public WindowSettings? WindowSettings { get; set; }

    public ModelSearchOptions? ModelSearchOptions { get; set; }

    /// <summary>
    /// Whether prompt auto completion is enabled
    /// </summary>
    public bool IsPromptCompletionEnabled { get; set; } = true;

    /// <summary>
    /// Relative path to the tag completion CSV file from 'LibraryDir/Tags'
    /// </summary>
    public string? TagCompletionCsv { get; set; }

    /// <summary>
    /// Whether to remove underscores from completions
    /// </summary>
    public bool IsCompletionRemoveUnderscoresEnabled { get; set; } = true;

    /// <summary>
    /// Format for Inference output image file names
    /// </summary>
    public string? InferenceOutputImageFileNameFormat { get; set; }

    /// <summary>
    /// Whether the Inference Image Viewer shows pixel grids at high zoom levels
    /// </summary>
    public bool IsImageViewerPixelGridEnabled { get; set; } = true;

    /// <summary>
    /// Whether Inference Image Browser delete action uses recycle bin if available
    /// </summary>
    public bool IsInferenceImageBrowserUseRecycleBinForDelete { get; set; } = true;

    public bool RemoveFolderLinksOnShutdown { get; set; }

    public bool IsDiscordRichPresenceEnabled { get; set; }

    [JsonIgnore]
    public Dictionary<string, string> DefaultEnvironmentVariables { get; } =
        new()
        {
            // Fixes potential setuptools error on Portable Windows Python
            ["SETUPTOOLS_USE_DISTUTILS"] = "stdlib",
            // Suppresses 'A new release of pip is available' messages
            ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1"
        };

    [JsonPropertyName("EnvironmentVariables")]
    public Dictionary<string, string>? UserEnvironmentVariables { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> EnvironmentVariables
    {
        get
        {
            if (UserEnvironmentVariables is null || UserEnvironmentVariables.Count == 0)
            {
                return DefaultEnvironmentVariables;
            }

            return DefaultEnvironmentVariables
                .Concat(UserEnvironmentVariables)
                .GroupBy(pair => pair.Key)
                // User variables override default variables with the same key
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Last().Value);
        }
    }

    public float AnimationScale { get; set; } = 1.0f;

    public bool AutoScrollLaunchConsoleToEnd { get; set; } = true;

    public int ConsoleLogHistorySize { get; set; } = 9001;

    public HashSet<int> FavoriteModels { get; set; } = new();

    public HashSet<TeachingTip> SeenTeachingTips { get; set; } = new();

    public Dictionary<NotificationKey, NotificationOption> NotificationOptions { get; set; } = new();

    public List<string> SelectedBaseModels { get; set; } =
        Enum.GetValues<CivitBaseModelType>()
            .Where(x => x != CivitBaseModelType.All)
            .Select(x => x.GetStringValue())
            .ToList();

    public Size InferenceImageSize { get; set; } = new(150, 190);

    [Obsolete("Use OutputsPageResizeFactor instead")]
    public Size OutputsImageSize { get; set; } = new(300, 300);
    public HolidayMode HolidayModeSetting { get; set; } = HolidayMode.Automatic;
    public bool IsWorkflowInfiniteScrollEnabled { get; set; } = true;
    public bool IsOutputsTreeViewEnabled { get; set; } = true;
    public CheckpointSortMode CheckpointSortMode { get; set; } = CheckpointSortMode.SharedFolderType;
    public ListSortDirection CheckpointSortDirection { get; set; } = ListSortDirection.Descending;
    public bool ShowModelsInSubfolders { get; set; } = true;
    public bool SortConnectedModelsFirst { get; set; } = true;
    public int ConsoleFontSize { get; set; } = 14;
    public bool AutoLoadCivitModels { get; set; } = true;

    /// <summary>
    /// When false, will copy files when drag/drop import happens
    /// Otherwise, it will move, as it states
    /// </summary>
    public bool MoveFilesOnImport { get; set; } = true;

    public bool DragMovesAllSelected { get; set; } = true;

    public bool HideEmptyRootCategories { get; set; }

    public bool HideInstalledModelsInModelBrowser { get; set; }

    public bool ShowNsfwInCheckpointsPage { get; set; }

    // public bool OptedInToInstallTelemetry { get; set; }

    public AnalyticsSettings Analytics { get; set; } = new();

    public double CheckpointsPageResizeFactor { get; set; } = 1.0d;

    public double OutputsPageResizeFactor { get; set; } = 1.0d;

    public double CivitBrowserResizeFactor { get; set; } = 1.0d;

    public bool HideEarlyAccessModels { get; set; }

    public string? ModelDirectoryOverride { get; set; } = null;

    [JsonIgnore]
    public bool IsHolidayModeActive =>
        HolidayModeSetting == HolidayMode.Automatic
            ? DateTimeOffset.Now.Month == 12
            : HolidayModeSetting == HolidayMode.Enabled;

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

    public void SetUpdateCheckDisabledForPackage(InstalledPackage package, bool disabled)
    {
        var installedPackage = InstalledPackages.FirstOrDefault(p => p.Id == package.Id);
        if (installedPackage != null)
        {
            installedPackage.DontCheckForUpdates = disabled;
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

        return supportedCultures.Contains(systemCulture.Name) ? systemCulture : new CultureInfo("en-US");
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal partial class SettingsSerializerContext : JsonSerializerContext;
