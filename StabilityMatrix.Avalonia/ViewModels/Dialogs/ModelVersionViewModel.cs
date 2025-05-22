﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class ModelVersionViewModel : ObservableObject
{
    private readonly IModelIndexService modelIndexService;

    [ObservableProperty]
    private CivitModelVersion modelVersion;

    [ObservableProperty]
    private ObservableCollection<CivitFileViewModel> civitFileViewModels;

    [ObservableProperty]
    private bool isInstalled;

    public ModelVersionViewModel(IModelIndexService modelIndexService, CivitModelVersion modelVersion)
    {
        this.modelIndexService = modelIndexService;

        ModelVersion = modelVersion;

        IsInstalled =
            ModelVersion.Files?.Any(file =>
                file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                && modelIndexService.ModelIndexBlake3Hashes.Contains(file.Hashes.BLAKE3)
            ) ?? false;

        CivitFileViewModels = new ObservableCollection<CivitFileViewModel>(
            (
                ModelVersion.Files?.Select(file => new CivitFileViewModel(modelIndexService, file)) ?? []
            ).OrderBy(a => a.CivitFile.Type == CivitFileType.TrainingData ? 1 : 0)
        );
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
