using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class ModelVersionViewModel : ObservableObject
{
    private readonly IModelIndexService modelIndexService;

    [ObservableProperty]
    public partial CivitModelVersion ModelVersion { get; set; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    public ModelVersionViewModel(IModelIndexService modelIndexService, CivitModelVersion modelVersion)
    {
        this.modelIndexService = modelIndexService;

        ModelVersion = modelVersion;

        IsInstalled =
            ModelVersion.Files?.Any(file =>
                file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                && modelIndexService.ModelIndexBlake3Hashes.Contains(file.Hashes.BLAKE3)
            ) ?? false;
    }

    public void RefreshInstallStatus()
    {
        IsInstalled =
            ModelVersion.Files?.Any(file =>
                file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                && modelIndexService.ModelIndexBlake3Hashes.Contains(file.Hashes.BLAKE3)
            ) ?? false;
    }
}
