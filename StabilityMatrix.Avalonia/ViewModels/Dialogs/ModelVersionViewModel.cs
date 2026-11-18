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

    [ObservableProperty]
    public partial CivitModelVersion ModelVersion { get; set; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsComponentsOnlyInstalled { get; set; }

    public ModelVersionViewModel(IModelIndexService modelIndexService, CivitModelVersion modelVersion)
    {
        this.modelIndexService = modelIndexService;

        ModelVersion = modelVersion;

        RefreshInstallStatus();

        EventManager.Instance.ModelIndexChanged += ModelIndexChanged;
    }

    public void RefreshInstallStatus()
    {
        var installState = ModelVersion.GetInstallState(modelIndexService.ModelIndexBlake3Hashes);
        IsInstalled = installState is CivitModelVersionInstallState.Installed;
        IsComponentsOnlyInstalled = installState is CivitModelVersionInstallState.ComponentsOnly;
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
