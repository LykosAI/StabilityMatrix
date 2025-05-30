using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitFileViewModel : DisposableViewModelBase
{
    private readonly IModelIndexService modelIndexService;
    private readonly Func<CivitFileViewModel, string?, Task>? downloadAction;

    [ObservableProperty]
    private CivitFile civitFile;

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    public required partial ObservableCollection<string> InstallLocations { get; set; }

    public CivitFileViewModel(
        IModelIndexService modelIndexService,
        CivitFile civitFile,
        Func<CivitFileViewModel, string?, Task>? downloadAction
    )
    {
        this.modelIndexService = modelIndexService;
        this.downloadAction = downloadAction;
        CivitFile = civitFile;
        IsInstalled =
            CivitFile is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
            && modelIndexService.ModelIndexBlake3Hashes.Contains(CivitFile.Hashes.BLAKE3);
        EventManager.Instance.ModelIndexChanged += ModelIndexChanged;
    }

    private void ModelIndexChanged(object? sender, EventArgs e)
    {
        IsInstalled =
            CivitFile is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
            && modelIndexService.ModelIndexBlake3Hashes.Contains(CivitFile.Hashes.BLAKE3);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDownload))]
    private async Task DownloadToDefaultAsync()
    {
        if (downloadAction != null)
        {
            await downloadAction(this, null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDownload))]
    private async Task DownloadToSelectedLocationAsync(string locationKey)
    {
        if (downloadAction != null)
        {
            await downloadAction(this, locationKey);
        }
    }

    private bool CanExecuteDownload()
    {
        return downloadAction != null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EventManager.Instance.ModelIndexChanged -= ModelIndexChanged;
        }
        base.Dispose(disposing);
    }
}
