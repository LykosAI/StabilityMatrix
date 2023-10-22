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
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(DownloadResourceDialog))]
[ManagedService]
[Transient]
public partial class DownloadResourceViewModel : ContentDialogViewModelBase
{
    private readonly IDownloadService downloadService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileNameWithHash))]
    private string? fileName;

    public string FileNameWithHash => $"{FileName} [{Resource.HashSha256.ToLowerInvariant()[..7]}]";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSizeText))]
    private long fileSize;

    [ObservableProperty]
    private RemoteResource resource;

    public string? FileSizeText => FileSize == 0 ? null : Size.FormatBase10Bytes(FileSize);

    public string ShortHash => Resource.HashSha256.ToLowerInvariant();

    public DownloadResourceViewModel(IDownloadService downloadService)
    {
        this.downloadService = downloadService;
    }

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

    public BetterContentDialog GetDialog()
    {
        return new BetterContentDialog
        {
            MinDialogWidth = 400,
            Title = "Download Model",
            Content = new DownloadResourceDialog { DataContext = this },
            PrimaryButtonText = Resources.Action_Continue,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary
        };
    }
}
