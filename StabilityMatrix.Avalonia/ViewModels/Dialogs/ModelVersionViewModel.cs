using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class ModelVersionViewModel : DisposableViewModelBase
{
    private readonly IModelIndexService modelIndexService;

    public string VersionDescription { get; set; }

    public bool HasVersionDescription { get; set; }

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

        CivitFileViewModels = new ObservableCollection<CivitFileViewModel>(
            ModelVersion.Files?.Select(file => new CivitFileViewModel(modelIndexService, file))
                ?? new List<CivitFileViewModel>()
        );

        EventManager.Instance.ModelIndexChanged += ModelIndexChanged;

        HasVersionDescription = !string.IsNullOrWhiteSpace(modelVersion.Description);
        VersionDescription =
            $"""<html><body class="markdown-body">{modelVersion.Description}</body></html>""";
    }

    public void RefreshInstallStatus()
    {
        IsInstalled =
            ModelVersion.Files?.Any(file =>
                file is { Type: CivitFileType.Model, Hashes.BLAKE3: not null }
                && modelIndexService.ModelIndexBlake3Hashes.Contains(file.Hashes.BLAKE3)
            ) ?? false;
    }

    private void ModelIndexChanged(object? sender, EventArgs e)
    {
        RefreshInstallStatus();
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
