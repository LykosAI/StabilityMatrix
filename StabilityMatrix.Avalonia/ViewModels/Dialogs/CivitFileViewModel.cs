using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitFileViewModel : DisposableViewModelBase
{
    private readonly IModelIndexService modelIndexService;

    [ObservableProperty]
    private CivitFile civitFile;

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    public required partial ObservableCollection<string> InstallLocations { get; set; }

    public CivitFileViewModel(IModelIndexService modelIndexService, CivitFile civitFile)
    {
        this.modelIndexService = modelIndexService;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EventManager.Instance.ModelIndexChanged -= ModelIndexChanged;
        }
        base.Dispose(disposing);
    }
}
