using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using Size = StabilityMatrix.Core.Helper.Size;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[ManagedService]
[RegisterTransient<SelectModelVersionViewModel>]
public partial class SelectModelVersionViewModel(
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IModelIndexService modelIndexService,
    INotificationService notificationService
) : ContentDialogViewModelBase
{
    private readonly IDownloadService downloadService = downloadService;

    public required ContentDialog Dialog { get; set; }

    public required string Description { get; set; }
    public new required string Title { get; set; }
    public required CivitModel CivitModel { get; set; }

    [ObservableProperty]
    public IReadOnlyList<ModelVersionViewModel> versions = [];

    [ObservableProperty]
    private Bitmap? previewImage;

    [ObservableProperty]
    private ModelVersionViewModel? selectedVersionViewModel;

    [ObservableProperty]
    private CivitFileViewModel? selectedFile;

    [ObservableProperty]
    private bool isImportEnabled;

    [ObservableProperty]
    private ObservableCollection<ImageSource> imageUrls = new();

    [ObservableProperty]
    private bool canGoToNextImage;

    [ObservableProperty]
    private bool canGoToPreviousImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedPageNumber))]
    private int selectedImageIndex;

    [ObservableProperty]
    private string importTooltip = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomSelected))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPathWarning))]
    private string selectedInstallLocation = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> availableInstallLocations = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPathWarning))]
    private string customInstallLocation = string.Empty;

    public bool IsCustomSelected => SelectedInstallLocation == "Custom...";
    public bool ShowEmptyPathWarning => IsCustomSelected && string.IsNullOrWhiteSpace(CustomInstallLocation);

    public int DisplayedPageNumber => SelectedImageIndex + 1;

    public override void OnLoaded()
    {
        SelectedVersionViewModel = Versions[0];
        CanGoToNextImage = true;

        // LoadInstallLocations() is called within OnSelectedFileChanged, which is triggered by OnSelectedVersionViewModelChanged.
        // However, to apply preferences correctly, we need AvailableInstallLocations populated first.
        // It might be better to ensure LoadInstallLocations is called before trying to apply preferences.
        // For now, we rely on the chain: OnLoaded -> sets SelectedVersionViewModel -> OnSelectedVersionViewModelChanged -> sets SelectedFile -> OnSelectedFileChanged -> LoadInstallLocations
        // Then, we apply preferences if available.
    }

    partial void OnSelectedVersionViewModelChanged(ModelVersionViewModel? value)
    {
        var nsfwEnabled = settingsManager.Settings.ModelBrowserNsfwEnabled;
        var allImages = value
            ?.ModelVersion?.Images?.Where(img => img.Type == "image" && (nsfwEnabled || img.NsfwLevel <= 1))
            ?.Select(x => new ImageSource(x.Url))
            .ToList();

        if (allImages == null || !allImages.Any())
        {
            // allImages = new List<ImageSource> { new(Assets.NoImage) };
            allImages = [];
            CanGoToNextImage = false;
        }
        else
        {
            CanGoToNextImage = allImages.Count > 1;
        }

        Dispatcher.UIThread.Post(() =>
        {
            CanGoToPreviousImage = false;
            // SelectedFile = SelectedVersionViewModel?.CivitFileViewModels.FirstOrDefault();
            ImageUrls = new ObservableCollection<ImageSource>(allImages);
            SelectedImageIndex = 0;

            // Apply saved preferences after SelectedFile change has potentially called LoadInstallLocations
            // It's crucial that LoadInstallLocations runs before this to populate AvailableInstallLocations
            // and set an initial SelectedInstallLocation.
            ApplySavedDownloadPreference();
        });
    }

    partial void OnSelectedFileChanged(CivitFileViewModel? value)
    {
        if (value is { IsInstalled: true }) { }

        var canImport = true;
        if (settingsManager.IsLibraryDirSet)
        {
            LoadInstallLocations();

            var fileSizeBytes = value?.CivitFile.SizeKb * 1024;
            var freeSizeBytes =
                SystemInfo.GetDiskFreeSpaceBytes(settingsManager.ModelsDirectory) ?? long.MaxValue;
            canImport = fileSizeBytes < freeSizeBytes;
            ImportTooltip = canImport
                ? "Free space after download: "
                    + (
                        freeSizeBytes < long.MaxValue
                            ? Size.FormatBytes(Convert.ToUInt64(freeSizeBytes - fileSizeBytes))
                            : "Unknown"
                    )
                : $"Not enough space on disk. Need {Size.FormatBytes(Convert.ToUInt64(fileSizeBytes))} but only have {Size.FormatBytes(Convert.ToUInt64(freeSizeBytes))}";
        }
        else
        {
            ImportTooltip = "Please set the library directory in settings";
        }

        IsImportEnabled = value?.CivitFile != null && canImport && !ShowEmptyPathWarning;
    }

    partial void OnSelectedInstallLocationChanged(string? value)
    {
        if (value?.Equals("Custom...", StringComparison.OrdinalIgnoreCase) is true)
        {
            // Only invoke the folder picker if a custom location isn't already set (e.g., by loading preferences).
            // If the user manually selects "Custom..." and CustomInstallLocation was previously cleared (due to a non-custom selection),
            // then string.IsNullOrWhiteSpace(this.CustomInstallLocation) will be true, and the dialog will show.
            if (string.IsNullOrWhiteSpace(this.CustomInstallLocation))
            {
                Dispatcher.UIThread.InvokeAsync(SelectCustomFolder);
            }
        }
        else
        {
            // If a non-custom location is selected, clear any existing custom path.
            CustomInstallLocation = string.Empty;
        }

        IsImportEnabled = !ShowEmptyPathWarning;
    }

    partial void OnCustomInstallLocationChanged(string value)
    {
        IsImportEnabled = !ShowEmptyPathWarning;
    }

    public void Cancel()
    {
        Dialog.Hide(ContentDialogResult.Secondary);
    }

    public void Import()
    {
        SaveCurrentDownloadPreference();
        Dialog.Hide(ContentDialogResult.Primary);
    }

    public async Task Delete()
    {
        if (SelectedFile == null)
            return;

        var fileToDelete = SelectedFile;
        var originalSelectedVersionVm = SelectedVersionViewModel;

        var hash = fileToDelete.CivitFile.Hashes.BLAKE3;
        if (string.IsNullOrWhiteSpace(hash))
        {
            notificationService.Show(
                "Error deleting file",
                "Could not delete model, hash is missing.",
                NotificationType.Error
            );
            return;
        }

        var matchingModels = (await modelIndexService.FindByHashAsync(hash)).ToList();

        if (matchingModels.Count == 0)
        {
            await modelIndexService.RefreshIndex();
            matchingModels = (await modelIndexService.FindByHashAsync(hash)).ToList();

            if (matchingModels.Count == 0)
            {
                notificationService.Show(
                    "Error deleting file",
                    "Could not delete model, model not found in index.",
                    NotificationType.Error
                );
                return;
            }
        }

        var dialog = new BetterContentDialog
        {
            Title = Resources.Label_AreYouSure,
            MaxDialogWidth = 750,
            MaxDialogHeight = 850,
            PrimaryButtonText = Resources.Action_Yes,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Close,
            Content =
                $"The following files:\n{string.Join('\n', matchingModels.Select(x => $"- {x.FileName}"))}\n"
                + "and all associated metadata files will be deleted. Are you sure?",
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            foreach (var localModel in matchingModels)
            {
                var checkpointPath = new FilePath(localModel.GetFullPath(settingsManager.ModelsDirectory));
                if (File.Exists(checkpointPath))
                {
                    File.Delete(checkpointPath);
                }

                var previewPath = localModel.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
                if (File.Exists(previewPath))
                {
                    File.Delete(previewPath);
                }

                var cmInfoPath = checkpointPath.ToString().Replace(checkpointPath.Extension, ".cm-info.json");
                if (File.Exists(cmInfoPath))
                {
                    File.Delete(cmInfoPath);
                }

                await modelIndexService.RemoveModelAsync(localModel);
            }
            fileToDelete.IsInstalled = false;
            originalSelectedVersionVm?.RefreshInstallStatus();
        }
    }

    public void PreviousImage()
    {
        if (SelectedImageIndex > 0)
            SelectedImageIndex--;
        CanGoToPreviousImage = SelectedImageIndex > 0;
        CanGoToNextImage = SelectedImageIndex < ImageUrls.Count - 1;
    }

    public void NextImage()
    {
        if (SelectedImageIndex < ImageUrls.Count - 1)
            SelectedImageIndex++;
        CanGoToPreviousImage = SelectedImageIndex > 0;
        CanGoToNextImage = SelectedImageIndex < ImageUrls.Count - 1;
    }

    public async Task SelectCustomFolder()
    {
        var files = await App.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select Download Folder",
                AllowMultiple = false,
                SuggestedStartLocation = await App.StorageProvider.TryGetFolderFromPathAsync(
                    Path.Combine(
                        settingsManager.ModelsDirectory,
                        CivitModel.Type.ConvertTo<SharedFolderType>().GetStringValue()
                    )
                ),
            }
        );

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            CustomInstallLocation = path;
            // Potentially save preference here if selection is considered final upon folder picking for custom.
            // However, saving on Import() is more robust as it's the explicit confirmation.
        }
    }

    private void LoadInstallLocations()
    {
        var installLocations = new ObservableCollection<string>();

        var rootModelsDirectory = new DirectoryPath(settingsManager.ModelsDirectory);

        var downloadDirectory = GetSharedFolderPath(
            rootModelsDirectory,
            SelectedFile?.CivitFile,
            CivitModel.Type,
            CivitModel.BaseModelType
        );

        if (!downloadDirectory.ToString().EndsWith("Unknown"))
        {
            installLocations.Add(
                Path.Combine("Models", Path.GetRelativePath(rootModelsDirectory, downloadDirectory))
            );
            foreach (
                var directory in downloadDirectory.EnumerateDirectories(
                    "*",
                    EnumerationOptionConstants.AllDirectories
                )
            )
            {
                installLocations.Add(
                    Path.Combine("Models", Path.GetRelativePath(rootModelsDirectory, directory))
                );
            }
        }

        if (downloadDirectory.ToString().EndsWith(SharedFolderType.DiffusionModels.GetStringValue()))
        {
            // also add StableDiffusion in case we have an AIO version
            var stableDiffusionDirectory = rootModelsDirectory.JoinDir(
                SharedFolderType.StableDiffusion.GetStringValue()
            );
            installLocations.Add(
                Path.Combine("Models", Path.GetRelativePath(rootModelsDirectory, stableDiffusionDirectory))
            );
        }

        installLocations.Add("Custom...");

        AvailableInstallLocations = installLocations;
        SelectedInstallLocation = installLocations.FirstOrDefault();
    }

    private static DirectoryPath GetSharedFolderPath(
        DirectoryPath rootModelsDirectory,
        CivitFile? civitFile,
        CivitModelType modelType,
        string? baseModelType
    )
    {
        if (civitFile?.Type is CivitFileType.VAE)
        {
            return rootModelsDirectory.JoinDir(SharedFolderType.VAE.GetStringValue());
        }

        if (
            modelType is CivitModelType.Checkpoint
            && (
                baseModelType == CivitBaseModelType.Flux1D.GetStringValue()
                || baseModelType == CivitBaseModelType.Flux1S.GetStringValue()
                || baseModelType == CivitBaseModelType.WanVideo.GetStringValue()
                || baseModelType == CivitBaseModelType.HunyuanVideo.GetStringValue()
                || civitFile?.Metadata.Format == CivitModelFormat.GGUF
            )
        )
        {
            return rootModelsDirectory.JoinDir(SharedFolderType.DiffusionModels.GetStringValue());
        }

        return rootModelsDirectory.JoinDir(modelType.ConvertTo<SharedFolderType>().GetStringValue());
    }

    private void ApplySavedDownloadPreference()
    {
        if (CivitModel.Type == null || !settingsManager.IsLibraryDirSet)
            return;

        var modelTypeKey = CivitModel.Type.ToString();
        if (
            settingsManager.Settings.ModelTypeDownloadPreferences.TryGetValue(
                modelTypeKey,
                out var preference
            )
        )
        {
            if (
                preference.SelectedInstallLocation == "Custom..."
                && !string.IsNullOrWhiteSpace(preference.CustomInstallLocation)
            )
            {
                // Ensure "Custom..." is an option or add it if necessary, though LoadInstallLocations should handle it.
                if (AvailableInstallLocations.Contains("Custom..."))
                {
                    CustomInstallLocation = preference.CustomInstallLocation ?? string.Empty;
                    SelectedInstallLocation = "Custom...";
                }
            }
            // If the saved SelectedInstallLocation is a custom path directly (legacy or direct set)
            // and it's not in AvailableInstallLocations, but CustomInstallLocation is set from preference.
            else if (
                !string.IsNullOrWhiteSpace(preference.CustomInstallLocation)
                && preference.SelectedInstallLocation == preference.CustomInstallLocation
            )
            {
                if (AvailableInstallLocations.Contains("Custom..."))
                {
                    CustomInstallLocation = preference.CustomInstallLocation ?? string.Empty;
                    SelectedInstallLocation = "Custom...";
                }
            }
            else if (
                preference.SelectedInstallLocation != null
                && AvailableInstallLocations.Contains(preference.SelectedInstallLocation)
            )
            {
                SelectedInstallLocation = preference.SelectedInstallLocation;
            }
        }
    }

    private void SaveCurrentDownloadPreference()
    {
        if (
            CivitModel?.Type == null
            || !settingsManager.IsLibraryDirSet
            || string.IsNullOrEmpty(SelectedInstallLocation)
        )
            return;

        var modelTypeKey = CivitModel.Type.ToString();
        var preference = new LastDownloadLocationInfo
        {
            SelectedInstallLocation = SelectedInstallLocation,
            CustomInstallLocation = IsCustomSelected ? CustomInstallLocation : null,
        };

        settingsManager.Transaction(s =>
        {
            s.ModelTypeDownloadPreferences[modelTypeKey] = preference;
        });
    }
}
