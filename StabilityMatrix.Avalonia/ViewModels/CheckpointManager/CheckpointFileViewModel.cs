using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Data;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointManager;

public partial class CheckpointFileViewModel : SelectableViewModelBase
{
    [ObservableProperty]
    private LocalModelFile checkpointFile;

    [ObservableProperty]
    private string thumbnailUri;

    [ObservableProperty]
    private ProgressReport? progress;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private long fileSize;

    [ObservableProperty]
    private string? noImageMessage;

    [ObservableProperty]
    private bool hideImage;

    [ObservableProperty]
    private DateTimeOffset lastModified;

    [ObservableProperty]
    private DateTimeOffset created;

    private readonly ISettingsManager settingsManager;
    private readonly IModelIndexService modelIndexService;
    private readonly INotificationService notificationService;
    private readonly IDownloadService downloadService;
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly ILogger logger;

    public bool CanShowTriggerWords => CheckpointFile.ConnectedModelInfo?.TrainedWords?.Length > 0;
    public string BaseModelName => CheckpointFile.ConnectedModelInfo?.BaseModel ?? string.Empty;
    public CivitModelType ModelType => CheckpointFile.ConnectedModelInfo?.ModelType ?? CivitModelType.Unknown;

    /// <inheritdoc/>
    public CheckpointFileViewModel(
        ISettingsManager settingsManager,
        IModelIndexService modelIndexService,
        INotificationService notificationService,
        IDownloadService downloadService,
        IServiceManager<ViewModelBase> vmFactory,
        ILogger logger,
        LocalModelFile checkpointFile
    )
    {
        this.settingsManager = settingsManager;
        this.modelIndexService = modelIndexService;
        this.notificationService = notificationService;
        this.downloadService = downloadService;
        this.vmFactory = vmFactory;
        this.logger = logger;
        CheckpointFile = checkpointFile;

        UpdateImage();

        if (!settingsManager.IsLibraryDirSet)
            return;

        AddDisposable(
            settingsManager.RegisterPropertyChangedHandler(
                s => s.ShowNsfwInCheckpointsPage,
                _ => Dispatcher.UIThread.Post(UpdateImage)
            )
        );

        FileSize = GetFileSize(CheckpointFile.GetFullPath(settingsManager.ModelsDirectory));
        LastModified = GetLastModified(CheckpointFile.GetFullPath(settingsManager.ModelsDirectory));
        Created = GetCreated(CheckpointFile.GetFullPath(settingsManager.ModelsDirectory));
    }

    [RelayCommand]
    private Task CopyTriggerWords()
    {
        if (!CheckpointFile.HasConnectedModel)
            return Task.CompletedTask;

        var words = CheckpointFile.ConnectedModelInfo.TrainedWordsString;
        return string.IsNullOrWhiteSpace(words) ? Task.CompletedTask : App.Clipboard.SetTextAsync(words);
    }

    [RelayCommand]
    private void FindOnModelBrowser()
    {
        if (CheckpointFile.ConnectedModelInfo?.ModelId == null)
            return;

        EventManager.Instance.OnNavigateAndFindCivitModelRequested(
            CheckpointFile.ConnectedModelInfo.ModelId.Value
        );
    }

    [RelayCommand]
    [Localizable(false)]
    private void OpenOnCivitAi()
    {
        if (CheckpointFile.ConnectedModelInfo?.ModelId == null)
            return;
        ProcessRunner.OpenUrl($"https://civitai.com/models/{CheckpointFile.ConnectedModelInfo.ModelId}");
    }

    [RelayCommand]
    [Localizable(false)]
    private Task CopyModelUrl()
    {
        if (!CheckpointFile.HasConnectedModel)
            return Task.CompletedTask;

        return CheckpointFile.ConnectedModelInfo.Source switch
        {
            ConnectedModelSource.Civitai when CheckpointFile.ConnectedModelInfo.ModelId == null
                => Task.CompletedTask,
            ConnectedModelSource.Civitai when CheckpointFile.ConnectedModelInfo.ModelId != null
                => App.Clipboard.SetTextAsync(
                    $"https://civitai.com/models/{CheckpointFile.ConnectedModelInfo.ModelId}"
                ),

            ConnectedModelSource.OpenModelDb
                => App.Clipboard.SetTextAsync(
                    $"https://openmodeldb.info/models/{CheckpointFile.ConnectedModelInfo.ModelName}"
                ),
            _ => Task.CompletedTask
        };
    }

    [RelayCommand]
    private async Task FindConnectedMetadata(bool forceReimport = false)
    {
        if (
            App.Services.GetService(typeof(IMetadataImportService))
            is not IMetadataImportService importService
        )
            return;

        IsLoading = true;

        try
        {
            var progressReport = new Progress<ProgressReport>(report =>
            {
                Progress = report;
            });

            var cmInfo = await importService.GetMetadataForFile(
                CheckpointFile.GetFullPath(settingsManager.ModelsDirectory),
                progressReport,
                forceReimport
            );
            if (cmInfo != null)
            {
                CheckpointFile.ConnectedModelInfo = cmInfo;

                var uri =
                    CheckpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory)
                    ?? cmInfo.ThumbnailImageUrl;
                if (string.IsNullOrWhiteSpace(uri))
                {
                    HideImage = true;
                    NoImageMessage = Resources.Label_NoImageFound;
                }
                else
                {
                    ThumbnailUri = uri;
                    HideImage = false;
                }

                await modelIndexService.RefreshIndex();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(bool showConfirmation = true)
    {
        var pathsToDelete = CheckpointFile
            .GetDeleteFullPaths(settingsManager.ModelsDirectory)
            .ToImmutableArray();

        if (pathsToDelete.IsEmpty)
            return;

        var confirmDeleteVm = vmFactory.Get<ConfirmDeleteDialogViewModel>();
        confirmDeleteVm.PathsToDelete = pathsToDelete;

        if (showConfirmation)
        {
            if (await confirmDeleteVm.GetDialog().ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        await using var delay = new MinimumDelay(200, 500);

        IsLoading = true;
        Progress = new ProgressReport(0f, "Deleting...");

        try
        {
            await confirmDeleteVm.ExecuteCurrentDeleteOperationAsync(failFast: true);
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent("Error deleting files", e.Message, NotificationType.Error);

            await modelIndexService.RefreshIndex();

            return;
        }
        finally
        {
            IsLoading = false;
        }

        await modelIndexService.RemoveModelAsync(CheckpointFile);
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        // Parent folder path
        var parentPath =
            Path.GetDirectoryName((string?)CheckpointFile.GetFullPath(settingsManager.ModelsDirectory)) ?? "";

        var textFields = new TextBoxField[]
        {
            new()
            {
                Label = "File name",
                Validator = text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        throw new DataValidationException("File name is required");

                    if (File.Exists(Path.Combine(parentPath, text)))
                        throw new DataValidationException("File name already exists");
                },
                Text = CheckpointFile.FileName
            }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Rename Model", "", textFields);

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var name = textFields[0].Text;
            var nameNoExt = Path.GetFileNameWithoutExtension(name);
            var originalNameNoExt = Path.GetFileNameWithoutExtension(CheckpointFile.FileName);
            // Rename file in OS
            try
            {
                var newFilePath = Path.Combine(parentPath, name);
                File.Move(CheckpointFile.GetFullPath(settingsManager.ModelsDirectory), newFilePath);

                // If preview image exists, rename it too
                var previewPath = CheckpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory);
                if (previewPath != null && File.Exists(previewPath))
                {
                    var newPreviewImagePath = Path.Combine(
                        parentPath,
                        $"{nameNoExt}.preview{Path.GetExtension(previewPath)}"
                    );
                    File.Move(previewPath, newPreviewImagePath);
                }

                // If connected model info exists, rename it too (<name>.cm-info.json)
                if (CheckpointFile.HasConnectedModel)
                {
                    var cmInfoPath = Path.Combine(parentPath, $"{originalNameNoExt}.cm-info.json");
                    if (File.Exists(cmInfoPath))
                    {
                        File.Move(cmInfoPath, Path.Combine(parentPath, $"{nameNoExt}.cm-info.json"));
                    }
                }

                await modelIndexService.RefreshIndex();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to rename checkpoint file");
            }
        }
    }

    [RelayCommand]
    private async Task OpenSafetensorMetadataViewer()
    {
        if (!CheckpointFile.SafetensorMetadataParsed)
        {
            if (
                !settingsManager.IsLibraryDirSet
                || new DirectoryPath(settingsManager.ModelsDirectory) is not { Exists: true } modelsDir
            )
            {
                return;
            }

            try
            {
                var safetensorPath = CheckpointFile.GetFullPath(modelsDir);

                var metadata = await SafetensorMetadata.ParseAsync(safetensorPath);

                CheckpointFile.SafetensorMetadataParsed = true;
                CheckpointFile.SafetensorMetadata = metadata;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse safetensor metadata");
                return;
            }
        }

        if (!CheckpointFile.SafetensorMetadataParsed)
        {
            return;
        }

        var vm = vmFactory.Get<SafetensorMetadataViewModel>(vm =>
        {
            vm.ModelName = CheckpointFile.DisplayModelName;
            vm.Metadata = CheckpointFile.SafetensorMetadata;
        });

        var dialog = vm.GetDialog();
        dialog.MinDialogHeight = 800;
        dialog.MinDialogWidth = 700;
        dialog.CloseButtonText = "Close";
        dialog.DefaultButton = ContentDialogButton.Close;

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task OpenMetadataEditor()
    {
        var vm = vmFactory.Get<ModelMetadataEditorDialogViewModel>(vm =>
        {
            vm.CheckpointFiles = [this];
        });

        var dialog = vm.GetDialog();
        dialog.MinDialogHeight = 800;
        dialog.MinDialogWidth = 700;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.IsFooterVisible = true;
        dialog.PrimaryButtonText = "Save";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = "Cancel";

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        // not supported yet
        if (vm.IsEditingMultipleCheckpoints)
            return;

        try
        {
            var hasCmInfoAlready = CheckpointFile.HasConnectedModel;
            var cmInfo = CheckpointFile.ConnectedModelInfo ?? new ConnectedModelInfo();
            var hasThumbnailChanged =
                vm.ThumbnailFilePath
                != CheckpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory);

            cmInfo.ModelName = vm.ModelName;
            cmInfo.ModelDescription = vm.ModelDescription;
            cmInfo.Nsfw = vm.IsNsfw;
            cmInfo.Tags = vm.Tags.Split(',').Select(x => x.Trim()).ToArray();
            cmInfo.BaseModel = vm.BaseModelType;
            cmInfo.TrainedWords = string.IsNullOrWhiteSpace(vm.TrainedWords)
                ? null
                : vm.TrainedWords.Split(',').Select(x => x.Trim()).ToArray();
            cmInfo.ThumbnailImageUrl = vm.ThumbnailFilePath;
            cmInfo.ModelType = vm.ModelType;
            cmInfo.VersionName = vm.VersionName;

            var modelFilePath = new FilePath(
                Path.Combine(settingsManager.ModelsDirectory, CheckpointFile.RelativePath)
            );

            IsLoading = true;
            Progress = new ProgressReport(0f, "Saving metadata...", isIndeterminate: true);

            if (!hasCmInfoAlready)
            {
                cmInfo.Hashes = new CivitFileHashes
                {
                    BLAKE3 = await FileHash.GetBlake3Async(
                        modelFilePath,
                        new Progress<ProgressReport>(report =>
                        {
                            Progress = report with { Title = "Calculating hash..." };
                        })
                    )
                };
                cmInfo.ImportedAt = DateTimeOffset.Now;
            }

            var modelFileName = modelFilePath.NameWithoutExtension;
            var modelFolder =
                modelFilePath.Directory
                ?? Path.Combine(settingsManager.ModelsDirectory, CheckpointFile.SharedFolderType.ToString());

            await cmInfo.SaveJsonToDirectory(modelFolder, modelFileName);

            if (string.IsNullOrWhiteSpace(cmInfo.ThumbnailImageUrl))
                return;

            if (File.Exists(cmInfo.ThumbnailImageUrl) && hasThumbnailChanged)
            {
                // delete existing preview image
                var existingPreviewPath = CheckpointFile.GetPreviewImageFullPath(
                    settingsManager.ModelsDirectory
                );
                if (existingPreviewPath != null && File.Exists(existingPreviewPath))
                {
                    File.Delete(existingPreviewPath);
                }

                var filePath = new FilePath(cmInfo.ThumbnailImageUrl);
                var previewPath = new FilePath(
                    modelFolder,
                    @$"{modelFileName}.preview{Path.GetExtension(cmInfo.ThumbnailImageUrl)}"
                );
                await filePath.CopyToAsync(previewPath);
            }
            else if (cmInfo.ThumbnailImageUrl.StartsWith("http"))
            {
                var imageExtension = Path.GetExtension(cmInfo.ThumbnailImageUrl).TrimStart('.');
                if (imageExtension is "jpg" or "jpeg" or "png" or "webp")
                {
                    var imageDownloadPath = modelFilePath.Directory!.JoinFile(
                        $"{modelFilePath.NameWithoutExtension}.preview.{imageExtension}"
                    );

                    var imageTask = downloadService.DownloadToFileAsync(
                        cmInfo.ThumbnailImageUrl,
                        imageDownloadPath,
                        new Progress<ProgressReport>(report =>
                        {
                            Progress = report with { Title = "Downloading image" };
                        })
                    );

                    await notificationService.TryAsync(imageTask, "Could not download preview image");
                }
            }

            await modelIndexService.RefreshIndex();
            notificationService.Show(
                "Metadata saved",
                "Metadata has been saved successfully",
                NotificationType.Success
            );
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to save metadata");
            notificationService.Show("Failed to save metadata", e.Message, NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    private DateTimeOffset GetLastModified(string filePath)
    {
        if (!File.Exists(filePath))
            return DateTimeOffset.MinValue;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.LastWriteTime;
    }

    private DateTimeOffset GetCreated(string filePath)
    {
        if (!File.Exists(filePath))
            return DateTimeOffset.MinValue;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.CreationTime;
    }

    private void UpdateImage()
    {
        if (
            !settingsManager.Settings.ShowNsfwInCheckpointsPage
            && CheckpointFile.ConnectedModelInfo?.Nsfw == true
        )
        {
            HideImage = true;
            NoImageMessage = Resources.Label_ImageHidden;
        }
        else
        {
            var previewPath = settingsManager.IsLibraryDirSet
                ? CheckpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory)
                    ?? CheckpointFile.ConnectedModelInfo?.ThumbnailImageUrl
                : null;

            if (string.IsNullOrWhiteSpace(previewPath))
            {
                HideImage = true;
                NoImageMessage = Resources.Label_NoImageFound;
            }
            else
            {
                ThumbnailUri = previewPath;
                HideImage = false;
            }
        }
    }
}
