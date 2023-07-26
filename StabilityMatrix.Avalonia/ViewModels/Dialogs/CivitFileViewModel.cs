using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class CivitFileViewModel : ObservableObject
{
    [ObservableProperty] private CivitFile civitFile;
    [ObservableProperty] private bool isInstalled;

    public CivitFileViewModel(HashSet<string> installedModelHashes, CivitFile civitFile)
    {
        CivitFile = civitFile;
        var lastIndexOfPeriod = CivitFile.Name.LastIndexOf(".", StringComparison.Ordinal);
        if (lastIndexOfPeriod > 0)
        {
            CivitFile.Name = CivitFile.Name[..lastIndexOfPeriod];
        }
        
        IsInstalled = CivitFile is {Type: CivitFileType.Model, Hashes.BLAKE3: not null} &&
                      installedModelHashes.Contains(CivitFile.Hashes.BLAKE3);
    }
}
