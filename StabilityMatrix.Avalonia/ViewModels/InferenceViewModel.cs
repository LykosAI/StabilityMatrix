using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(InferencePage))]
public partial class InferenceViewModel : PageViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly INotificationService notificationService;
    // private readonly IRelayCommandFactory commandFactory;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IApiFactory apiFactory;

    public override string Title => "Inference";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.AppGeneric, IsFilled = true };

    public RefreshBadgeViewModel ConnectionBadge { get; } =
        new()
        {
            State = ProgressState.Failed,
            FailToolTipText = "Not connected",
            FailIcon = FluentAvalonia.UI.Controls.Symbol.Refresh,
            SuccessToolTipText = "Connected",
        };

    public IInferenceClientManager ClientManager { get; }

    public AvaloniaList<InferenceTabViewModelBase> Tabs { get; } = new();

    [ObservableProperty]
    private LoadableViewModelBase? selectedTab;

    [ObservableProperty]
    private PackagePair? runningPackage;
    
    public InferenceViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        IApiFactory apiFactory,
        INotificationService notificationService,
//         IRelayCommandFactory commandFactory,
        IInferenceClientManager inferenceClientManager,
        ISettingsManager settingsManager
    )
    {
        this.vmFactory = vmFactory;
        this.apiFactory = apiFactory;
        this.notificationService = notificationService;
//         this.commandFactory = commandFactory;
        this.settingsManager = settingsManager;

        ClientManager = inferenceClientManager;
        
        // Keep RunningPackage updated with the current package pair
        EventManager.Instance.RunningPackageStatusChanged += (_, args) =>
        {
            RunningPackage = args.CurrentPackagePair;
        };

        MenuSaveAsCommand.WithNotificationErrorHandler(notificationService);
    }

    public override void OnLoaded()
    {
        if (Tabs.Count == 0)
        {
            AddTab();
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
        Tabs.Add(vmFactory.Get<InferenceTextToImageViewModel>());
    }

    /// <summary>
    /// When the close button on the tab is clicked, remove the tab.
    /// </summary>
    public void OnTabCloseRequested(TabViewTabCloseRequestedEventArgs e)
    {
        if (e.Item is not InferenceTabViewModelBase vm)
        {
            Logger.Warn("Tab close requested for unknown item {@Item}", e);
            return;
        }
        
        Logger.Trace("Closing tab {Title}", vm.TabTitle);
        Tabs.Remove(vm);
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
        
        if (RunningPackage is not null)
        {
            await notificationService.TryAsync(
                ClientManager.ConnectAsync(RunningPackage),
                "Could not connect to backend"
            );
        }
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

        await notificationService.TryAsync(
            ClientManager.CloseAsync(),
            "Could not disconnect from ComfyUI backend"
        );
    }

    /// <summary>
    /// Menu "Save As" command.
    /// </summary>
    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task MenuSaveAs()
    {
        var currentTab = SelectedTab;
        if (currentTab == null)
        {
            Logger.Warn("MenuSaveAs: currentTab is null");
            return;
        }

        // Prompt for save file dialog
        var provider = App.StorageProvider;

        var projectDir = new DirectoryPath(settingsManager.LibraryDir, "Projects");
        projectDir.Create();
        var startDir = await provider.TryGetFolderFromPathAsync(projectDir);

        var result = await provider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save As",
                SuggestedFileName = "Untitled",
                FileTypeChoices = new FilePickerFileType[]
                {
                    new("StabilityMatrix Project")
                    {
                        Patterns = new[] { "*.smproj" },
                        MimeTypes = new[] { "application/json" },
                    }
                },
                SuggestedStartLocation = startDir,
                DefaultExtension = ".smproj",
                ShowOverwritePrompt = true,
            }
        );

        if (result is null)
        {
            Logger.Trace("MenuSaveAs: user cancelled");
            return;
        }
        
        var document = InferenceProjectDocument.FromLoadable(currentTab);

        // Save to file
        try
        {
            await using var stream = await result.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(
                stream,
                document,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent(
                "Could not save to file", 
                $"[{e.GetType().Name}] {e.Message}", 
                NotificationType.Error);
            return;
        }
        
        notificationService.Show(
            "Saved",
            $"Saved project to {result.Name}",
            NotificationType.Success
        );
    }
    
    /*public AsyncRelayCommand MenuOpenProjectCommand =>
        commandFactory.CreateWithNotificationErrorHandling(MenuOpenProject);*/
    
    /// <summary>
    /// Menu "Open Project" command.
    /// </summary>
    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task MenuOpenProject()
    {
        // Prompt for open file dialog
        var provider = App.StorageProvider;

        var projectDir = new DirectoryPath(settingsManager.LibraryDir, "Projects");
        projectDir.Create();
        var startDir = await provider.TryGetFolderFromPathAsync(projectDir);

        var results = await provider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Project File",
                FileTypeFilter = new FilePickerFileType[]
                {
                    new("StabilityMatrix Project")
                    {
                        Patterns = new[] { "*.smproj" },
                        MimeTypes = new[] { "application/json" },
                    }
                },
                SuggestedStartLocation = startDir,
            }
        );

        if (results.Count == 0)
        {
            Logger.Trace("MenuOpenProject: No files selected");
            return;
        }

        // Load from file
        var file = results[0];
        await using var stream = await file.OpenReadAsync();

        var document = await JsonSerializer.DeserializeAsync<InferenceProjectDocument>(stream);
        if (document is null)
        {
            Logger.Warn("MenuOpenProject: Deserialize project file returned null");
            return;
        }

        InferenceTabViewModelBase? vm = null;
        if (document.ProjectType is InferenceProjectType.TextToImage && document.State is not null)
        {
            var textToImage = vmFactory.Get<InferenceTextToImageViewModel>();
            textToImage.LoadStateFromJsonObject(document.State);
            vm = textToImage;
        }

        if (vm == null)
        {
            Logger.Warn("MenuOpenProject: Unknown project type");
            return;
        }

        Tabs.Add(vm);
        
        // Set the selected tab to the newly opened tab
        SelectedTab = vm;
    }
}
