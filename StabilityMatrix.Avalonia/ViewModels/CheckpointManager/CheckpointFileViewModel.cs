using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
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

    public bool CanShowTriggerWords => CheckpointFile.ConnectedModelInfo?.TrainedWords?.Length > 0;
    public string BaseModelName => CheckpointFile.ConnectedModelInfo?.BaseModel ?? string.Empty;
    public CivitModelType ModelType => CheckpointFile.ConnectedModelInfo?.ModelType ?? CivitModelType.Unknown;

    /// <inheritdoc/>
    public CheckpointFileViewModel(
        ISettingsManager settingsManager,
        IModelIndexService modelIndexService,
        LocalModelFile checkpointFile
    )
    {
        this.settingsManager = settingsManager;
        this.modelIndexService = modelIndexService;
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
        if (showConfirmation)
        {
            var confirmationDialog = new BetterContentDialog
            {
                Title = Resources.Label_AreYouSure,
                Content = Resources.Label_ActionCannotBeUndone,
                PrimaryButtonText = Resources.Action_Delete,
                SecondaryButtonText = Resources.Action_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                IsSecondaryButtonEnabled = true,
            };
            var dialogResult = await confirmationDialog.ShowAsync();
            if (dialogResult != ContentDialogResult.Primary)
                return;
        }

        EventManager.Instance.OnDeleteModelRequested(this, CheckpointFile.RelativePath);
    }
}
