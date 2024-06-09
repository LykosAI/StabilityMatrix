using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
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

    private readonly ISettingsManager settingsManager;
    private readonly IModelIndexService modelIndexService;
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    public bool CanShowTriggerWords => CheckpointFile.ConnectedModelInfo?.TrainedWords?.Length > 0;
    public string BaseModelName => CheckpointFile.ConnectedModelInfo?.BaseModel ?? string.Empty;
    public CivitModelType ModelType => CheckpointFile.ConnectedModelInfo?.ModelType ?? CivitModelType.Unknown;

    /// <inheritdoc/>
    public CheckpointFileViewModel(
        ISettingsManager settingsManager,
        IModelIndexService modelIndexService,
        INotificationService notificationService,
        ServiceManager<ViewModelBase> vmFactory,
        LocalModelFile checkpointFile
    )
    {
        this.settingsManager = settingsManager;
        this.modelIndexService = modelIndexService;
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;
        CheckpointFile = checkpointFile;
        ThumbnailUri = settingsManager.IsLibraryDirSet
            ? CheckpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory)
                ?? CheckpointFile.ConnectedModelInfo?.ThumbnailImageUrl
                ?? Assets.NoImage.ToString()
            : string.Empty;
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

        EventManager.Instance.OnNavigateAndFindCivitModelRequested(CheckpointFile.ConnectedModelInfo.ModelId);
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
        return CheckpointFile.ConnectedModelInfo?.ModelId == null
            ? Task.CompletedTask
            : App.Clipboard.SetTextAsync(
                $"https://civitai.com/models/{CheckpointFile.ConnectedModelInfo.ModelId}"
            );
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
                ThumbnailUri =
                    CheckpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory)
                    ?? cmInfo.ThumbnailImageUrl
                    ?? Assets.NoImage.ToString();

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
}
