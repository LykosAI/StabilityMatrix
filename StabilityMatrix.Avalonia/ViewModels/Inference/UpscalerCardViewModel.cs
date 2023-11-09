using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(UpscalerCard))]
[ManagedService]
[Transient]
public partial class UpscalerCardViewModel : LoadableViewModelBase
{
    private readonly INotificationService notificationService;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    private double scale = 2;

    [ObservableProperty]
    private ComfyUpscaler? selectedUpscaler = ComfyUpscaler.Defaults[0];

    public IInferenceClientManager ClientManager { get; }

    public UpscalerCardViewModel(
        IInferenceClientManager clientManager,
        INotificationService notificationService,
        ITrackedDownloadService trackedDownloadService,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.notificationService = notificationService;
        this.trackedDownloadService = trackedDownloadService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;

        ClientManager = clientManager;
    }

    [RelayCommand]
    private async Task RemoteDownload(ComfyUpscaler? upscaler)
    {
        if (upscaler?.DownloadableResource is not { } resource)
            return;

        var sharedFolderType =
            resource.ContextType as SharedFolderType?
            ?? throw new InvalidOperationException("ContextType is not SharedFolderType");

        var confirmDialog = vmFactory.Get<DownloadResourceViewModel>();
        confirmDialog.Resource = resource;
        confirmDialog.FileName = upscaler.Value.Name;

        if (await confirmDialog.GetDialog().ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var modelsDir = new DirectoryPath(settingsManager.ModelsDirectory).JoinDir(
            sharedFolderType.GetStringValue()
        );

        var download = trackedDownloadService.NewDownload(
            resource.Url,
            modelsDir.JoinFile(upscaler.Value.Name)
        );
        download.ContextAction = new ModelPostDownloadContextAction();
        download.Start();

        EventManager.Instance.OnToggleProgressFlyout();
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<UpscalerCardModel>(state);

        Scale = model.Scale;
        SelectedUpscaler = model.SelectedUpscaler;
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new UpscalerCardModel { Scale = Scale, SelectedUpscaler = SelectedUpscaler }
        );
    }
}
