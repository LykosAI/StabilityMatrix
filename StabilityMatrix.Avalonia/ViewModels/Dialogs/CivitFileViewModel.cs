using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitFileViewModel : ObservableObject
{
    [ObservableProperty] private CivitFile civitFile;
    [ObservableProperty] private bool isInstalled;

    public CivitFileViewModel(ISettingsManager settingsManager, CivitFile civitFile)
    {
        CivitFile = civitFile;
        
        var installedModelHashes = settingsManager.Settings.InstalledModelHashes;
        IsInstalled = CivitFile is {Type: CivitFileType.Model, Hashes.BLAKE3: not null} &&
                      installedModelHashes.Contains(CivitFile.Hashes.BLAKE3);
    }
}
