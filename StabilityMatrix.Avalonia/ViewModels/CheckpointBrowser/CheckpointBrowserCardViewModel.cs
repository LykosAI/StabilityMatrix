using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

[ManagedService]
[Transient]
public partial class CheckpointBrowserCardViewModel : Base.ProgressViewModel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IDownloadService downloadService;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly INotificationService notificationService;
    private readonly IModelIndexService modelIndexService;
    private readonly IModelImportService modelImportService;

    public Action<CheckpointBrowserCardViewModel>? OnDownloadStart { get; set; }

    public CivitModel CivitModel
    {
        get => civitModel;
        set
        {
            civitModel = value;
            IsFavorite = settingsManager.Settings.FavoriteModels.Contains(value.Id);
            UpdateImage();
            CheckIfInstalled();
        }
    }
    private CivitModel civitModel;

    public override bool IsTextVisible => Value > 0;

    [ObservableProperty]
    private Uri? cardImage;

    [ObservableProperty]
    private bool isImporting;

    [ObservableProperty]
    private string updateCardText = string.Empty;

    [ObservableProperty]
    private bool showUpdateCard;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private bool showSantaHats = true;

    public CheckpointBrowserCardViewModel(
        IDownloadService downloadService,
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> dialogFactory,
        INotificationService notificationService,
        IModelIndexService modelIndexService,
        IModelImportService modelImportService
    )
    {
        this.downloadService = downloadService;
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationService = notificationService;
        this.modelIndexService = modelIndexService;
        this.modelImportService = modelImportService;

        // Update image when nsfw setting changes
        settingsManager.RegisterPropertyChangedHandler(
            s => s.ModelBrowserNsfwEnabled,
            _ => Dispatcher.UIThread.Post(UpdateImage)
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

        if (CivitModel.ModelVersions == null)
            return;

        var installedModels = modelIndexService.ModelIndexBlake3Hashes;
        if (installedModels.Count == 0)
            return;

        // check if latest version is installed
        var latestVersion = CivitModel.ModelVersions.FirstOrDefault();
        if (latestVersion == null)
            return;

        var latestVersionInstalled =
            latestVersion.Files != null
            && latestVersion.Files.Any(
                file =>
                    file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                    && installedModels.Contains(file.Hashes.BLAKE3)
            );

        // check if any of the ModelVersion.Files.Hashes.BLAKE3 hashes are in the installedModels list
        var anyVersionInstalled =
            latestVersionInstalled
            || CivitModel.ModelVersions.Any(
                version =>
                    version.Files != null
                    && version.Files.Any(
                        file =>
                            file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                            && installedModels.Contains(file.Hashes.BLAKE3)
                    )
            );

        UpdateCardText = latestVersionInstalled
            ? "Installed"
            : anyVersionInstalled
                ? "Update Available"
                : string.Empty;

        ShowUpdateCard = anyVersionInstalled;
    }

    private void UpdateImage()
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var version = CivitModel.ModelVersions?.FirstOrDefault();
        var images = version?.Images;

        // Try to find a valid image
        var image = images
            ?.Where(
                img => LocalModelFile.SupportedImageExtensions.Any(img.Url.Contains) && img.Type == "image"
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
        ProcessRunner.OpenUrl($"https://civitai.com/models/{CivitModel.Id}");
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
    private async Task ShowVersionDialog(CivitModel model)
    {
        var versions = model.ModelVersions;
        if (versions is null || versions.Count == 0)
        {
            notificationService.Show(
                new Notification(
                    "Model has no versions available",
                    "This model has no versions available for download",
                    NotificationType.Warning
                )
            );
            return;
        }

        var dialog = new BetterContentDialog
        {
            Title = model.Name,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            CloseOnClickOutside = true,
            MaxDialogWidth = 750,
            MaxDialogHeight = 950,
        };

        var prunedDescription = Utilities.RemoveHtml(model.Description);

        var viewModel = dialogFactory.Get<SelectModelVersionViewModel>();
        viewModel.Dialog = dialog;
        viewModel.Title = model.Name;
        viewModel.Description = prunedDescription;
        viewModel.CivitModel = model;
        viewModel.Versions = versions
            .Select(version => new ModelVersionViewModel(modelIndexService, version))
            .ToImmutableArray();
        viewModel.SelectedVersionViewModel = viewModel.Versions[0];

        dialog.Content = new SelectModelVersionDialog { DataContext = viewModel };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var selectedVersion = viewModel?.SelectedVersionViewModel?.ModelVersion;
        var selectedFile = viewModel?.SelectedFile?.CivitFile;

        DirectoryPath downloadPath;
        if (viewModel?.IsCustomSelected is true)
        {
            downloadPath = viewModel.CustomInstallLocation;
        }
        else
        {
            var subFolder =
                viewModel?.SelectedInstallLocation
                ?? Path.Combine(@"Models", model.Type.ConvertTo<SharedFolderType>().GetStringValue());
            downloadPath = Path.Combine(settingsManager.LibraryDir, subFolder);
        }

        await Task.Delay(100);
        await DoImport(model, downloadPath, selectedVersion, selectedFile);
    }

    private async Task DoImport(
        CivitModel model,
        DirectoryPath downloadFolder,
        CivitModelVersion? selectedVersion = null,
        CivitFile? selectedFile = null
    )
    {
        IsImporting = true;
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
        var modelFile =
            selectedFile ?? modelVersion.Files?.FirstOrDefault(x => x.Type == CivitFileType.Model);
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

<<<<<<< HEAD
        // Folders might be missing if user didn't install any packages yet
        downloadFolder.Create();

        var downloadPath = downloadFolder.JoinFile(modelFile.Name);

        // Download model info and preview first
        var cmInfoPath = await SaveCmInfo(model, modelVersion, modelFile, downloadFolder);
        var previewImagePath = await SavePreviewImage(modelVersion, downloadPath);

        // Create tracked download
        var download = trackedDownloadService.NewDownload(modelFile.DownloadUrl, downloadPath);

        // Add hash info
        download.ExpectedHashSha256 = modelFile.Hashes.SHA256;

        // Add files to cleanup list
        download.ExtraCleanupFileNames.Add(cmInfoPath);
        if (previewImagePath is not null)
        {
            download.ExtraCleanupFileNames.Add(previewImagePath);
        }

        // Attach for progress updates
        download.ProgressUpdate += (s, e) =>
        {
            Value = e.Percentage;
            if (e.Type == ProgressType.Hashing)
            {
                Text = $"Validating... {e.Percentage}%";
            }
            else
            {
                Text = $"Downloading... {e.Percentage}%";
            }
        };

        download.ProgressStateChanged += (s, e) =>
        {
            if (e == ProgressState.Success)
=======
        await modelImportService.DoImport(
            model,
            downloadFolder,
            modelVersion,
            modelFile,
            onImportComplete: () =>
>>>>>>> b47b8f65 (Merge pull request #787 from ionite34/fix-invalid-filenames)
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
            });
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlRegex();
}
