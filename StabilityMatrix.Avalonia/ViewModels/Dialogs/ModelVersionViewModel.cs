using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class ModelVersionViewModel : ObservableObject
{
    [ObservableProperty] private CivitModelVersion modelVersion;
    [ObservableProperty] private ObservableCollection<CivitFileViewModel> civitFileViewModels;
    [ObservableProperty] private bool isInstalled;

    public ModelVersionViewModel(ISettingsManager settingsManager, CivitModelVersion modelVersion)
    {
        ModelVersion = modelVersion;

        var installedModelHashes = settingsManager.Settings.InstalledModelHashes;
        IsInstalled = ModelVersion.Files?.Any(file =>
                          file is {Type: CivitFileType.Model, Hashes.BLAKE3: not null} &&
                          installedModelHashes.Contains(file.Hashes.BLAKE3)) ??
                      false;

        CivitFileViewModels = new ObservableCollection<CivitFileViewModel>(
            ModelVersion.Files?.Select(file => new CivitFileViewModel(settingsManager, file)) ??
            new List<CivitFileViewModel>());
    }
}
