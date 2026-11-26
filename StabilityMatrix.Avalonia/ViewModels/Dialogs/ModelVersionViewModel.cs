using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class ModelVersionViewModel : DisposableViewModelBase
{
    private readonly IModelIndexService modelIndexService;
    private readonly CivitModelType modelType;

    public string VersionDescription { get; set; }

    public bool HasVersionDescription { get; set; }

    [ObservableProperty]
    public partial CivitModelVersion ModelVersion { get; set; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    public ModelVersionViewModel(
        IModelIndexService modelIndexService,
        CivitModelVersion modelVersion,
        CivitModelType modelType
    )
    {
        this.modelIndexService = modelIndexService;
        this.modelType = modelType;

        ModelVersion = modelVersion;

        RefreshInstallStatus();

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
        IsInstalled = ModelVersion.IsInstalled(modelType, modelIndexService.ModelIndexBlake3Hashes);
    }

    private void ModelIndexChanged(object? sender, EventArgs e)
    {
        // Dispatch to UI thread since the event may be raised from a background thread
        Dispatcher.UIThread.Post(RefreshInstallStatus);
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
