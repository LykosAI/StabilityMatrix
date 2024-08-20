using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(UpscalerCard))]
[ManagedService]
[Transient]
public partial class UpscalerCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "Upscaler";

    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    private double scale = 2;

    [ObservableProperty]
    private ComfyUpscaler? selectedUpscaler = ComfyUpscaler.Defaults[0];

    public IInferenceClientManager ClientManager { get; }

    public UpscalerCardViewModel(
        IInferenceClientManager clientManager,
        INotificationService notificationService,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;

        ClientManager = clientManager;
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
        return SerializeModel(new UpscalerCardModel { Scale = Scale, SelectedUpscaler = SelectedUpscaler });
    }
}
