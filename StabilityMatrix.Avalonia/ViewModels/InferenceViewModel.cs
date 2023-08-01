using System;
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
using StabilityMatrix.Core.Inference;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InferencePage))]
public partial class InferenceViewModel : PageViewModelBase, IDisposable
{
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IApiFactory apiFactory;
    
    public override string Title => "Inference";
    public override IconSource IconSource => new SymbolIconSource
        {Symbol = Symbol.AppGeneric, IsFilled = true};
    
    public ComfyClient? Client { get; set; }
    public bool IsConnected => Client is not null;
    
    public AvaloniaList<ViewModelBase> Tabs { get; } = new();
    
    [ObservableProperty]
    private ViewModelBase? selectedTab;

    public InferenceViewModel(ServiceManager<ViewModelBase> vmFactory, IApiFactory apiFactory, INotificationService notificationService)
    {
        this.vmFactory = vmFactory;
        this.apiFactory = apiFactory;
        this.notificationService = notificationService;
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
        if (Client is not null)
        {
            notificationService.Show("Already connected", "ComfyUI backend is already connected");
            return;
        }
        // TODO: make address configurable
        Client = new ComfyClient(apiFactory, new Uri("http://127.0.0.1:8188"));
        await Client.ConnectAsync();
        
        // Update status
        OnPropertyChanged(nameof(IsConnected));
    }
    
    /// <summary>
    /// Disconnect from the inference server.
    /// </summary>
    [RelayCommand]
    private async Task Disconnect()
    {
        if (Client is null)
        {
            notificationService.Show("Not connected", "ComfyUI backend is not connected");
            return;
        }
        await Client.CloseAsync();
        Client.Dispose();
        Client = null;
        
        // Update status
        OnPropertyChanged(nameof(IsConnected));
    }
    
    public void Dispose()
    {
        Client?.Dispose();
        Client = null;
        GC.SuppressFinalize(this);
    }
}
