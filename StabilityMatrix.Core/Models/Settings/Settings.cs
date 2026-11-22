using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Semver;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Api.CivArchive;
using StabilityMatrix.Core.Models.Api.HuggingFace;
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

    /// <summary>
    /// The app version the "What's New" changelog was last shown for.
    /// Null on a fresh install / first run of the feature.
    /// </summary>
    public string? LastSeenChangelogVersion { get; set; }

    /// <summary>
    /// Whether to show the "What's New" notice after the app is updated
    /// </summary>
    public bool ShowReleaseNotesAfterUpdate { get; set; } = true;

    // UI states
    public bool ModelBrowserNsfwEnabled { get; set; }
    public bool IsNavExpanded { get; set; }
    public bool IsImportAsConnected { get; set; }
    public bool ShowConnectedModelImages { get; set; }
    public WindowSettings? WindowSettings { get; set; }

    // HuggingFace browser UI prefs
    public bool HuggingFaceUseStructuredView { get; set; } = true;
    public bool HuggingFaceHideInstalled { get; set; }
    public HuggingFaceSearchSort HuggingFaceSearchSort { get; set; } = HuggingFaceSearchSort.Downloads;

    public ModelSearchOptions? ModelSearchOptions { get; set; }
    public CivArchiveBrowserOptions CivArchiveBrowserOptions { get; set; } = new();

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
    /// Use the classic dropdown menu instead of the gallery dialog when adding a new Inference tab
    /// </summary>
    public bool UseClassicInferenceTabPicker { get; set; }

    /// <summary>
    /// Whether Inference Image Browser delete action uses recycle bin if available
    /// </summary>
    public bool IsInferenceImageBrowserUseRecycleBinForDelete { get; set; } = true;

    public bool RemoveFolderLinksOnShutdown { get; set; }

    public bool IsDiscordRichPresenceEnabled { get; set; }

    /// <summary>
    /// When true, vertical scrollbars stay permanently visible at their expanded
    /// (thicker) thickness instead of fading to a thin auto-hiding bar. Default
    /// matches the app's preferred behavior; users can toggle back to Avalonia's
    /// auto-hide default via the Appearance settings.
    /// </summary>
    public bool ScrollBarsAlwaysVisible { get; set; } = true;

    /// <summary>
    /// Enables the app-wide quick-hide hotkey that instantly minimizes the main window
    /// or exits the app, per <see cref="QuickHideMode"/>.
    /// </summary>
    public bool IsQuickHideHotkeyEnabled { get; set; }

    /// <summary>
    /// What the quick-hide hotkey does when pressed.
    /// </summary>
    public QuickHideMode QuickHideMode { get; set; } = QuickHideMode.Minimize;

    /// <summary>
    /// Gesture string for the quick-hide hotkey, e.g. "Ctrl+Shift+H".
    /// The Ctrl token maps to Cmd (⌘) on macOS.
    /// </summary>
    public string QuickHideHotkeyGesture { get; set; } = "Ctrl+Shift+H";

    public HashSet<string> DisabledBaseModelTypes { get; set; } = [];

    public HashSet<string> SavedInferenceDimensions { get; set; } =
        [
            "1024 x 1024",
            "1152 x 896",
            "1216 x 832",
            "1280 x 720",
            "1344 x 768",
            "1536 x 640",
            "768 x 768",
            "512 x 512",
            "640 x 1536",
            "768 x 1344",
            "720 x 1280",
            "832 x 1216",
            "896 x 1152",
        ];

    /// <summary>
    /// Size limit in GB for the uv package cache. When set, the cache size is checked at
    /// startup and a warning notification is shown when exceeded (or the cache is purged,
    /// if <see cref="IsUvCacheAutoPurgeEnabled"/>). 0 (the default) disables the startup
    /// check entirely — the size scan is expensive on large caches, so it is opt-in.
    /// </summary>
    public int UvCacheMaxSizeGb { get; set; }

    /// <summary>
    /// When the uv cache exceeds <see cref="UvCacheMaxSizeGb"/> at startup, purge it
    /// automatically instead of only warning.
    /// </summary>
    public bool IsUvCacheAutoPurgeEnabled { get; set; }

    [JsonIgnore]
    public Dictionary<string, string> DefaultEnvironmentVariables { get; } =
        new()
        {
            // Fixes potential setuptools error on Portable Windows Python
            // ["SETUPTOOLS_USE_DISTUTILS"] = "stdlib",
            // Suppresses 'A new release of pip is available' messages
            ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1",
        };

    [JsonPropertyName("EnvironmentVariables")]
    public Dictionary<string, string>? UserEnvironmentVariables { get; set; }

    public List<EnvVarKeyPair>? UserEnvironmentVariablesList { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> EnvironmentVariables
    {
        get
        {
            // add here when can use GlobalConfig
            DefaultEnvironmentVariables["UV_CACHE_DIR"] = Path.Combine(
                GlobalConfig.LibraryDir,
                "Assets",
                "uv",
                "cache"
            );

            // Prefer new list format, fall back to legacy dict
            var userVars = UserEnvironmentVariablesList is { Count: > 0 }
                ? UserEnvironmentVariablesList
                    .Where(kvp => kvp.IsEnabled && !string.IsNullOrWhiteSpace(kvp.Key))
                    .GroupBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal)
                : UserEnvironmentVariables;

            if (userVars is null || userVars.Count == 0)
            {
                return DefaultEnvironmentVariables;
            }

            return DefaultEnvironmentVariables
                .Concat(userVars)
                .GroupBy(pair => pair.Key)
                // User variables override default variables with the same key
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Last().Value);
        }
    }

    /// <summary>
    /// True if the user has explicitly configured this key under Settings > Environment
    /// Variables (and it's enabled, when using the list format). Packages should check this
    /// before setting a computed default/workaround env var, so a value the user configured
    /// themselves (e.g. to work around an issue on their machine) is never silently overwritten.
    /// </summary>
    public bool IsEnvironmentVariableUserOverridden(string key)
    {
        if (UserEnvironmentVariablesList is { Count: > 0 } list)
        {
            return list.Any(kvp => kvp.IsEnabled && string.Equals(kvp.Key, key, StringComparison.Ordinal));
        }

        return UserEnvironmentVariables?.ContainsKey(key) == true;
    }

    public float AnimationScale { get; set; } = 1.0f;

    public bool AutoScrollLaunchConsoleToEnd { get; set; } = true;

    public int ConsoleLogHistorySize { get; set; } = 9001;

    public HashSet<int> FavoriteModels { get; set; } = new();

    public HashSet<TeachingTip> SeenTeachingTips { get; set; } = new();

    public Dictionary<NotificationKey, NotificationOption> NotificationOptions { get; set; } = new();

    /// <summary>
    /// IDs of server-pushed notifications the user has dismissed.
    /// </summary>
    public List<string> DismissedNotificationIds { get; set; } = [];

    public List<string> SelectedBaseModels { get; set; } = [];
    public List<string> SelectedCivitBaseModels { get; set; } = [];

    public Size InferenceImageSize { get; set; } = new(150, 190);

    [Obsolete("Use OutputsPageResizeFactor instead")]
    public Size OutputsImageSize { get; set; } = new(300, 300);
    public HolidayMode HolidayModeSetting { get; set; } = HolidayMode.Automatic;

    /// <summary>
    /// Minimum level of messages written to the log file and console
    /// </summary>
    public AppLogLevel LogLevel { get; set; } = DefaultLogLevel;

    /// <summary>
    /// Log level used until settings are loaded, and for settings files without a <see cref="LogLevel"/>
    /// </summary>
    public const AppLogLevel DefaultLogLevel = AppLogLevel.Debug;
    public bool IsWorkflowInfiniteScrollEnabled { get; set; } = true;
    public bool IsOutputsTreeViewEnabled { get; set; } = true;
    public CheckpointSortMode CheckpointSortMode { get; set; } = CheckpointSortMode.SharedFolderType;
    public ListSortDirection CheckpointSortDirection { get; set; } = ListSortDirection.Descending;
    public bool ShowModelsInSubfolders { get; set; } = true;
    public bool ShowWorkflowsInSubfolders { get; set; } = true;
    public bool SortConnectedModelsFirst { get; set; } = true;
    public int ConsoleFontSize { get; set; } = 14;
    public bool AutoLoadCivitModels { get; set; } = true;

    [JsonPropertyName("UseLegacyModelSearch")]
    public bool UseLegacySearch { get; set; }

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

    public double CivArchiveBrowserResizeFactor { get; set; } = 1.0d;

    /// <summary>Content zoom factor for the in-app documentation viewer (1.0 = 100%).</summary>
    public double DocumentationZoomFactor { get; set; } = 1.0d;

    public bool CivArchiveBrowserFitCardImages { get; set; } = true;

    public bool HideEarlyAccessModels { get; set; }

    public bool CivitUseDiscoveryApi { get; set; }

    public string? ModelDirectoryOverride { get; set; } = null;

    public GpuInfo? PreferredGpu { get; set; }

    public int MaxConcurrentDownloads { get; set; }

    public bool FilterExtraNetworksByBaseModel { get; set; } = true;

    public bool ShowAllAvailablePythonVersions { get; set; }

    public bool IsMainWindowSidebarOpen { get; set; }

    public Dictionary<string, LastDownloadLocationInfo> ModelTypeDownloadPreferences { get; set; } = new();

    public bool ShowTrainingDataInModelBrowser { get; set; }

    public string? CivitModelBrowserFileNamePattern { get; set; }

    public string? ModelOrganizationFileNamePattern { get; set; }

    public bool ModelPickerIsGridView { get; set; }
    public Dictionary<string, ModelPickerFilterState> ModelPickerFilterStates { get; set; } = [];
    public CheckpointSortMode ModelPickerSortMode { get; set; } = CheckpointSortMode.Title;
    public ListSortDirection ModelPickerSortDirection { get; set; } = ListSortDirection.Ascending;

    /// <summary>
    /// User-defined model categories (name + colour). Membership is stored per model, see
    /// <see cref="UserModelMetadata"/>.
    /// </summary>
    public List<ModelCategory> ModelCategories { get; set; } = [];

    /// <summary>
    /// Category names selected in the Checkpoint Manager filter (any-of match).
    /// </summary>
    public List<string> CheckpointsSelectedCategories { get; set; } = [];

    /// <summary>
    /// Whether the Checkpoint Manager filter includes models with no categories.
    /// </summary>
    public bool CheckpointsIncludeUncategorized { get; set; }

    public int InferenceDimensionStepChange { get; set; } = 128;

    /// <summary>
    /// Last-used Inference dock layouts (serialized Dock layout JSON) keyed by
    /// <see cref="Inference.InferenceProjectType"/> name, applied to new tabs of that type.
    /// </summary>
    public Dictionary<string, string> InferenceDockLayouts { get; set; } = [];

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
[JsonSerializable(typeof(LastDownloadLocationInfo))]
[JsonSerializable(typeof(Dictionary<string, LastDownloadLocationInfo>))]
[JsonSerializable(typeof(ModelPickerFilterState))]
[JsonSerializable(typeof(Dictionary<string, ModelPickerFilterState>))]
[JsonSerializable(typeof(ModelCategory))]
[JsonSerializable(typeof(List<ModelCategory>))]
[JsonSerializable(typeof(CivArchiveBrowserOptions))]
internal partial class SettingsSerializerContext : JsonSerializerContext;
