using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[ManagedService]
[RegisterTransient<CheckpointBrowserCardViewModel>]
public partial class CheckpointBrowserCardViewModel : ProgressViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IDownloadService downloadService;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly IServiceManager<ViewModelBase> dialogFactory;
    private readonly INotificationService notificationService;
    private readonly IModelIndexService modelIndexService;
    private readonly IModelImportService modelImportService;
    private readonly ILiteDbContext liteDbContext;
    private readonly CivitCompatApiManager civitApi;
    private readonly INavigationService<MainWindowViewModel> navigationService;

    public Action<CheckpointBrowserCardViewModel>? OnDownloadStart { get; set; }

    public CivitModel CivitModel
    {
        get => civitModel;
        set
        {
            civitModel = value;
            IsFavorite = settingsManager.Settings.FavoriteModels.Contains(value.Id);
            UpdateImage();
            UpdateDisplayVersion();
            CheckIfInstalled();
        }
    }
    private CivitModel civitModel;

    public int Order { get; set; }

    public override bool IsTextVisible => Value > 0;

    [ObservableProperty]
    private Uri? cardImage;

    [ObservableProperty]
    private bool isImporting;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string updateCardText = string.Empty;

    [ObservableProperty]
    private bool showUpdateCard;

    [ObservableProperty]
    private bool isEarlyAccessUpdate;

    /// <summary>
    /// Version name shown on the card. Follows the same Hide Early Access filtering as the
    /// card image so the two always describe the same version.
    /// </summary>
    [ObservableProperty]
    private string displayVersionName = string.Empty;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private bool showSantaHats = true;

    public CheckpointBrowserCardViewModel(
        IDownloadService downloadService,
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService,
        IModelIndexService modelIndexService,
        IModelImportService modelImportService,
        ILiteDbContext liteDbContext,
        CivitCompatApiManager civitApi,
        INavigationService<MainWindowViewModel> navigationService
    )
    {
        this.downloadService = downloadService;
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;
        this.modelIndexService = modelIndexService;
        this.modelImportService = modelImportService;
        this.liteDbContext = liteDbContext;
        this.civitApi = civitApi;
        this.navigationService = navigationService;

        // Update image when nsfw setting changes
        AddDisposable(
            settingsManager.RegisterPropertyChangedHandler(
                s => s.ModelBrowserNsfwEnabled,
                _ => Dispatcher.UIThread.Post(UpdateImage)
            ),
            settingsManager.RegisterPropertyChangedHandler(
                s => s.HideEarlyAccessModels,
                _ =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateImage();
                        UpdateDisplayVersion();
                        CheckIfInstalled();
                    })
            )
        );

        ShowSantaHats = settingsManager.Settings.IsHolidayModeActive;
    }

    private void CheckIfInstalled()
    {
        if (Design.IsDesignMode)
        {
            UpdateCardText = "Installed";
            ShowUpdateCard = true;
            return;
        }

        UpdateCardText = string.Empty;
        ShowUpdateCard = false;
        IsEarlyAccessUpdate = false;

        if (CivitModel.ModelVersions == null)
            return;

        var installedModels = modelIndexService.ModelIndexBlake3Hashes;
        if (installedModels.Count == 0)
            return;

        // Versions hidden by the Hide Early Access setting don't participate in the
        // installed/update state either - a hidden version can't be an "update"
        var hideEarlyAccessModels = settingsManager.Settings.HideEarlyAccessModels;
        var versions = CivitModel
            .ModelVersions.Where(v => !hideEarlyAccessModels || !v.IsEarlyAccess)
            .ToList();
        if (versions.Count == 0)
            return;

        bool IsVersionInstalled(CivitModelVersion version) =>
            version.Files?.Any(file =>
                file is { Hashes.BLAKE3: not null }
                && file.Type.IsDownloadableModelFile()
                && installedModels.Contains(file.Hashes.BLAKE3)
            ) == true;

        var installedVersionIds = versions.Where(IsVersionInstalled).Select(v => v.Id).ToHashSet();
        if (installedVersionIds.Count == 0)
            return;

        // Multi-architecture models publish parallel tracks in one version list; a newer
        // version only counts as an update for an installed version on the same base-model
        // track (a Pony release is not an update to an Illustrious file)
        var newerUninstalled = new Dictionary<int, CivitModelVersion>();
        foreach (var installedVersion in versions.Where(v => installedVersionIds.Contains(v.Id)))
        {
            var track = string.IsNullOrWhiteSpace(installedVersion.BaseModel)
                ? versions
                : versions
                    .Where(v =>
                        string.Equals(
                            v.BaseModel,
                            installedVersion.BaseModel,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .ToList();

            var installedIndex = track.FindIndex(v => v.Id == installedVersion.Id);
            foreach (
                var newer in track
                    .Take(Math.Max(installedIndex, 0))
                    .Where(v => !installedVersionIds.Contains(v.Id))
            )
            {
                newerUninstalled[newer.Id] = newer;
            }
        }

        ShowUpdateCard = true;

        if (newerUninstalled.Count == 0)
        {
            UpdateCardText = "Installed";
        }
        else if (newerUninstalled.Values.All(v => v.IsEarlyAccess))
        {
            UpdateCardText = Resources.Label_EarlyAccess;
            IsEarlyAccessUpdate = true;
        }
        else
        {
            UpdateCardText = "Update Available";
        }
    }

    private CivitModelVersion? GetDisplayedVersion()
    {
        var hideEarlyAccessModels = settingsManager.Settings.HideEarlyAccessModels;
        return CivitModel.ModelVersions?.FirstOrDefault(v => !hideEarlyAccessModels || !v.IsEarlyAccess);
    }

    private void UpdateDisplayVersion()
    {
        DisplayVersionName = GetDisplayedVersion()?.Name ?? string.Empty;
    }

    private void UpdateImage()
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var version = GetDisplayedVersion();
        var images = version?.Images;

        // Try to find a valid image
        var image = images
            ?.Where(img =>
                LocalModelFile.SupportedImageExtensions.Any(img.Url.Contains) && img.Type == "image"
            )
            .FirstOrDefault(image => nsfwEnabled || image.NsfwLevel <= 1);
        if (image != null)
        {
            CardImage = new Uri(image.Url);
            return;
        }

        // If no valid image found, use no image
        CardImage = Assets.NoImage;
    }

    [RelayCommand]
    private void OpenModel()
    {
        ProcessRunner.OpenUrl(CivitaiUrlHelper.GetModelUrl(CivitModel.Id, CivitModel.Nsfw));
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (settingsManager.Settings.FavoriteModels.Contains(CivitModel.Id))
        {
            settingsManager.Transaction(s => s.FavoriteModels.Remove(CivitModel.Id));
        }
        else
        {
            settingsManager.Transaction(s => s.FavoriteModels.Add(CivitModel.Id));
        }

        IsFavorite = settingsManager.Settings.FavoriteModels.Contains(CivitModel.Id);
    }

    [RelayCommand]
    public void SearchAuthor()
    {
        EventManager.Instance.OnNavigateAndFindCivitAuthorRequested(CivitModel.Creator?.Username);
    }

    private async Task DoImport(
        CivitModel model,
        DirectoryPath downloadFolder,
        CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null
    )
    {
        IsImporting = true;
        IsLoading = true;
        Text = "Downloading...";

        OnDownloadStart?.Invoke(this);

        // Get latest version
        var modelVersion = selectedVersion ?? model.ModelVersions?.FirstOrDefault();
        if (modelVersion is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            Text = "Unable to Download";
            return;
        }

        // Get latest version file
        var modelFile = selectedFile ?? modelVersion.Files?.FirstOrDefault(x => x.Type.IsModelWeights());
        if (modelFile is null)
        {
            notificationService.Show(
                new Notification(
                    "Model has no files available",
                    "This model has no files available for download",
                    NotificationType.Warning
                )
            );
            Text = "Unable to Download";
            return;
        }

        await modelImportService.DoImport(
            model,
            downloadFolder,
            modelVersion,
            modelFile,
            onImportComplete: () =>
            {
                Text = "Import Complete";

                IsIndeterminate = false;
                Value = 100;
                CheckIfInstalled();
                DelayedClearProgress(TimeSpan.FromMilliseconds(800));

                return Task.CompletedTask;
            },
            onImportCanceled: () =>
            {
                Text = "Cancelled";
                DelayedClearProgress(TimeSpan.FromMilliseconds(500));

                return Task.CompletedTask;
            },
            onImportFailed: () =>
            {
                Text = "Download Failed";
                DelayedClearProgress(TimeSpan.FromMilliseconds(800));

                return Task.CompletedTask;
            }
        );
    }

    private void DelayedClearProgress(TimeSpan delay)
    {
        Task.Delay(delay)
            .ContinueWith(_ =>
            {
                Text = string.Empty;
                Value = 0;
                IsImporting = false;
                IsLoading = false;
            });
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlRegex();
}
