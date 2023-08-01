using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InferencePage))]
public partial class InferenceViewModel : PageViewModelBase
{
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IApiFactory apiFactory;

    public override string Title => "Inference";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.AppGeneric, IsFilled = true };

    public IInferenceClientManager ClientManager { get; }

    public AvaloniaList<ViewModelBase> Tabs { get; } = new();

    [ObservableProperty]
    private ViewModelBase? selectedTab;

    public InferenceViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        IApiFactory apiFactory,
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager
    )
    {
        this.vmFactory = vmFactory;
        this.apiFactory = apiFactory;
        this.notificationService = notificationService;
        ClientManager = inferenceClientManager;
    }

    private InferenceTextToImageViewModel CreateTextToImageViewModel()
    {
        return vmFactory.Get<InferenceTextToImageViewModel>(vm =>
        {
            vm.Parent = this;
        });
    }

    public override void OnLoaded()
    {
        if (Tabs.Count == 0)
        {
            Tabs.Add(CreateTextToImageViewModel());
        }

        // Select first tab if none is selected
        if (SelectedTab is null && Tabs.Count > 0)
        {
            SelectedTab = Tabs[0];
        }

        base.OnLoaded();
    }

    /// <summary>
    /// When the + button on the tab control is clicked, add a new tab.
    /// </summary>
    [RelayCommand]
    private void AddTab()
    {
        Tabs.Add(CreateTextToImageViewModel());
    }

    /// <summary>
    /// Connect to the inference server.
    /// </summary>
    [RelayCommand]
    private async Task Connect()
    {
        if (ClientManager.IsConnected)
        {
            notificationService.Show("Already connected", "ComfyUI backend is already connected");
            return;
        }
        // TODO: make address configurable
        await ClientManager.ConnectAsync();
    }

    /// <summary>
    /// Disconnect from the inference server.
    /// </summary>
    [RelayCommand]
    private async Task Disconnect()
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show("Not connected", "ComfyUI backend is not connected");
            return;
        }

        await ClientManager.CloseAsync();
    }
}
