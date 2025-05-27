using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitFileViewModel : ObservableObject
{
    [ObservableProperty]
    private CivitFile civitFile;

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    public required partial ObservableCollection<string> InstallLocations { get; set; }

    public CivitFileViewModel(IModelIndexService modelIndexService, CivitFile civitFile)
    {
        CivitFile = civitFile;
        IsInstalled =
            CivitFile is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
            && modelIndexService.ModelIndexBlake3Hashes.Contains(CivitFile.Hashes.BLAKE3);
    }
}
