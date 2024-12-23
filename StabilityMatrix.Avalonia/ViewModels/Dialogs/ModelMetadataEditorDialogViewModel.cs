using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ModelMetadataEditorDialog))]
[ManagedService]
[RegisterTransient<ModelMetadataEditorDialogViewModel>]
public partial class ModelMetadataEditorDialogViewModel(ISettingsManager settingsManager)
    : ContentDialogViewModelBase,
        IDropTarget
{
    [ObservableProperty]
    private List<CheckpointFileViewModel> checkpointFiles = [];

    [ObservableProperty]
    private string modelName = string.Empty;

    [ObservableProperty]
    private string modelDescription = string.Empty;

    [ObservableProperty]
    private bool isNsfw;

    [ObservableProperty]
    private string tags = string.Empty;

    [ObservableProperty]
    private CivitModelType modelType = CivitModelType.Other;

    [ObservableProperty]
    private string versionName = string.Empty;

    [ObservableProperty]
    private CivitBaseModelType baseModelType = CivitBaseModelType.Other;

    [ObservableProperty]
    private string trainedWords = string.Empty;

    [ObservableProperty]
    private string thumbnailFilePath = string.Empty;

    public bool IsEditingMultipleCheckpoints => CheckpointFiles.Count > 1;

    [RelayCommand]
    private async Task OpenFilePickerDialog()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select an image",
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            }
        );

        if (files.Count == 0)
            return;

        var sourceFile = new FilePath(files[0].TryGetLocalPath()!);

        ThumbnailFilePath = sourceFile.FullPath;
    }

    partial void OnCheckpointFilesChanged(List<CheckpointFileViewModel> value)
    {
        if (IsEditingMultipleCheckpoints)
            return;

        var firstCheckpoint = CheckpointFiles.FirstOrDefault();
        if (firstCheckpoint == null)
            return;

        if (!firstCheckpoint.CheckpointFile.HasConnectedModel)
        {
            ModelName = firstCheckpoint.CheckpointFile.DisplayModelName;
            ThumbnailFilePath = GetImagePath(firstCheckpoint.CheckpointFile);
            BaseModelType = CivitBaseModelType.Other;
            ModelType = CivitModelType.Other;
            return;
        }

        if (
            EnumExtensions.TryParseEnumStringValue(
                firstCheckpoint.CheckpointFile.ConnectedModelInfo.BaseModel,
                CivitBaseModelType.Other,
                out var baseModel
            )
        )
        {
            BaseModelType = baseModel;
        }

        ModelName = firstCheckpoint.CheckpointFile.ConnectedModelInfo.ModelName;
        ModelDescription = firstCheckpoint.CheckpointFile.ConnectedModelInfo.ModelDescription;
        IsNsfw = firstCheckpoint.CheckpointFile.ConnectedModelInfo.Nsfw;
        Tags = string.Join(", ", firstCheckpoint.CheckpointFile.ConnectedModelInfo.Tags);
        ModelType = firstCheckpoint.CheckpointFile.ConnectedModelInfo.ModelType;
        VersionName = firstCheckpoint.CheckpointFile.ConnectedModelInfo.VersionName;
        TrainedWords =
            firstCheckpoint.CheckpointFile.ConnectedModelInfo.TrainedWords == null
                ? string.Empty
                : string.Join(", ", firstCheckpoint.CheckpointFile.ConnectedModelInfo.TrainedWords);
        ThumbnailFilePath = GetImagePath(firstCheckpoint.CheckpointFile);
    }

    private string GetImagePath(LocalModelFile checkpointFile)
    {
        return checkpointFile.HasConnectedModel
            ? checkpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory)
                ?? checkpointFile.ConnectedModelInfo?.ThumbnailImageUrl
                ?? Assets.NoImage.ToString()
            : checkpointFile.GetPreviewImageFullPath(settingsManager.ModelsDirectory)
                ?? Assets.NoImage.ToString();
    }

    public void DragOver(object? sender, DragEventArgs e)
    {
        if (
            e.Data.GetDataFormats().Contains(DataFormats.Files)
            || e.Data.GetContext<LocalImageFile>() is not null
        )
        {
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
    }

    public void Drop(object? sender, DragEventArgs e)
    {
        if (
            e.Data.GetFiles() is not { } files
            || files.Select(f => f.TryGetLocalPath()).FirstOrDefault() is not { } path
        )
        {
            return;
        }

        e.Handled = true;
        ThumbnailFilePath = path;
    }
}
