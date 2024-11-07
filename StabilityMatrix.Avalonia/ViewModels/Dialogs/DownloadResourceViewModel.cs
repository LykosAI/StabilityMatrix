using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(DownloadResourceDialog))]
[ManagedService]
[Transient]
public partial class DownloadResourceViewModel(
    IDownloadService downloadService,
    ISettingsManager settingsManager,
    ITrackedDownloadService trackedDownloadService
) : ContentDialogViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileNameWithHash))]
    private string? fileName;

    public string FileNameWithHash => $"{FileName} [{Resource.HashSha256?.ToLowerInvariant()[..7]}]";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSizeText))]
    private long fileSize;

    [ObservableProperty]
    private RemoteResource resource;

    public string? FileSizeText => FileSize == 0 ? null : Size.FormatBase10Bytes(FileSize);

    public string? ShortHash => Resource.HashSha256?.ToLowerInvariant()[..7];

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        // Get download size
        if (!Design.IsDesignMode && Resource.Url is { } url)
        {
            FileSize = await downloadService.GetFileSizeAsync(url.ToString());
        }
    }

    [RelayCommand]
    private void OpenInfoUrl()
    {
        if (Resource.InfoUrl is { } url)
        {
            ProcessRunner.OpenUrl(url);
        }
    }

    public TrackedDownload StartDownload()
    {
        var sharedFolderType =
            Resource.ContextType as SharedFolderType?
            ?? throw new InvalidOperationException("ContextType is not SharedFolderType");

        var modelsDir = new DirectoryPath(settingsManager.ModelsDirectory).JoinDir(
            sharedFolderType.GetStringValue()
        );

        if (Resource.RelativeDirectory is not null)
        {
            modelsDir = modelsDir.JoinDir(Resource.RelativeDirectory);
        }

        var download = trackedDownloadService.NewDownload(
            Resource.Url,
            modelsDir.JoinFile(Resource.FileName)
        );

        // Set extraction properties
        download.AutoExtractArchive = Resource.AutoExtractArchive;
        download.ExtractRelativePath = Resource.ExtractRelativePath;

        download.ContextAction = new ModelPostDownloadContextAction();
        trackedDownloadService.TryStartDownload(download);

        EventManager.Instance.OnToggleProgressFlyout();

        return download;
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.MinDialogWidth = 400;
        dialog.Title = "Download Model";
        dialog.Content = new DownloadResourceDialog { DataContext = this };
        dialog.PrimaryButtonText = Resources.Action_Continue;
        dialog.CloseButtonText = Resources.Action_Cancel;
        dialog.DefaultButton = ContentDialogButton.Primary;

        return dialog;
    }
}
