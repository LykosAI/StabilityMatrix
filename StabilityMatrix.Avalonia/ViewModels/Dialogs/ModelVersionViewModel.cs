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

        EventManager.Instance.ModelIndexChanged += ModelIndexChanged;
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
