using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using InferenceTabViewModelBase = StabilityMatrix.Avalonia.ViewModels.Base.InferenceTabViewModelBase;
using Path = System.IO.Path;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[Preload]
[View(typeof(InferencePage))]
[RegisterSingleton<InferenceViewModel>]
public partial class InferenceViewModel : PageViewModelBase, IAsyncDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly INotificationService notificationService;

    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IModelIndexService modelIndexService;
    private readonly ILiteDbContext liteDbContext;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly RunningPackageService runningPackageService;
    private Guid? selectedPackageId;
    private List<IServiceScope> scopes = [];

    public override string Title => Resources.Label_Inference;
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.AppGeneric, IconVariant = IconVariant.Filled };

    public RefreshBadgeViewModel ConnectionBadge { get; } =
        new()
        {
            State = ProgressState.Failed,
            FailToolTipText = "Not connected",
            FailIcon = FluentAvalonia.UI.Controls.Symbol.Refresh,
            SuccessToolTipText = Resources.Label_Connected,
        };

    public IInferenceClientManager ClientManager { get; }

    public SharedState SharedState { get; }

    public ObservableCollection<InferenceTabViewModelBase> Tabs { get; } = new();

    [ObservableProperty]
    private InferenceTabViewModelBase? selectedTab;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private bool isWaitingForConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComfyRunning))]
    private PackagePair? runningPackage;

    public bool IsComfyRunning => RunningPackage?.BasePackage is ComfyUI;

    private IDisposable? onStartupComplete;

    public InferenceViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ISettingsManager settingsManager,
        IModelIndexService modelIndexService,
        ILiteDbContext liteDbContext,
        IServiceScopeFactory scopeFactory,
        RunningPackageService runningPackageService,
        SharedState sharedState
    )
    {
        this.vmFactory = vmFactory;
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.modelIndexService = modelIndexService;
        this.liteDbContext = liteDbContext;
        this.scopeFactory = scopeFactory;
        this.runningPackageService = runningPackageService;

        ClientManager = inferenceClientManager;
        SharedState = sharedState;

        // Keep RunningPackage updated with the current package pair
        runningPackageService.RunningPackages.CollectionChanged += RunningPackagesOnCollectionChanged;

        // "Send to Inference"
        EventManager.Instance.InferenceProjectRequested += InstanceOnInferenceProjectRequested;

        // Global requests for custom prompt queueing
        EventManager.Instance.InferenceQueueCustomPrompt += OnInferenceQueueCustomPromptRequested;

        MenuSaveAsCommand.WithConditionalNotificationErrorHandler(notificationService);
        MenuOpenProjectCommand.WithConditionalNotificationErrorHandler(notificationService);
    }

    private Task InstanceOnInferenceProjectRequested(
        object? sender,
        LocalImageFile imageFile,
        InferenceProjectType type
    ) => Dispatcher.UIThread.InvokeAsync(async () => await AddTabFromFileAsync(imageFile, type));

    private void DisconnectFromComfy()
    {
        RunningPackage = null;

        // Cancel any pending connection
        if (ConnectCancelCommand.CanExecute(null))
        {
            ConnectCancelCommand.Execute(null);
        }
        onStartupComplete?.Dispose();
        onStartupComplete = null;
        IsWaitingForConnection = false;

        // Disconnect
        Logger.Trace("On package close - disconnecting");
        DisconnectCommand.Execute(null);
    }

    /// <summary>
    /// Updates the RunningPackage property when the running package changes.
    /// Also starts a connection to the backend if a new ComfyUI package is running.
    /// And disconnects if the package is closed.
    /// </summary>
    private void RunningPackagesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (
            e.NewItems?.OfType<KeyValuePair<Guid, RunningPackageViewModel>>().Select(x => x.Value)
            is not { } newItems
        )
        {
            if (RunningPackage != null)
            {
                DisconnectFromComfy();
            }
            return;
        }

        var comfyViewModel = newItems.FirstOrDefault(
            vm =>
                vm.RunningPackage.InstalledPackage.Id == selectedPackageId
                || vm.RunningPackage.BasePackage is ComfyUI
        );

        if (comfyViewModel is null && RunningPackage?.BasePackage is ComfyUI)
        {
            DisconnectFromComfy();
        }
        else if (comfyViewModel != null && RunningPackage == null)
        {
            IsWaitingForConnection = true;
            RunningPackage = comfyViewModel.RunningPackage;
            onStartupComplete = Observable
                .FromEventPattern<string>(
                    comfyViewModel.RunningPackage.BasePackage,
                    nameof(comfyViewModel.RunningPackage.BasePackage.StartupComplete)
                )
                .Take(1)
                .Subscribe(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (ConnectCommand.CanExecute(null))
                        {
                            Logger.Trace("On package launch - starting connection");
                            ConnectCommand.Execute(null);
                        }

                        IsWaitingForConnection = false;
                    });
                });
        }
    }

    private void OnInferenceQueueCustomPromptRequested(object? sender, InferenceQueueCustomPromptEventArgs e)
    {
        // Get currently selected tab
        var currentTab = SelectedTab;

        if (currentTab is InferenceGenerationViewModelBase generationViewModel)
        {
            Dispatcher
                .UIThread.InvokeAsync(async () =>
                {
                    await generationViewModel.RunCustomGeneration(e);
                })
                .SafeFireAndForget(ex =>
                {
                    Logger.Error(ex, "Failed to queue prompt");

                    Dispatcher.UIThread.Post(() =>
                    {
                        notificationService.ShowPersistent(
                            "Failed to queue prompt",
                            $"{ex.GetType().Name}: {ex.Message}",
                            NotificationType.Error
                        );
                    });
                });
        }
    }

    public override void OnLoaded()
    {
        base.OnLoaded();

        modelIndexService.BackgroundRefreshIndex();
    }

    protected override async Task OnInitialLoadedAsync()
    {
        await base.OnInitialLoadedAsync();

        if (Design.IsDesignMode)
            return;

        // Load any open projects
        var openProjects = await liteDbContext.InferenceProjects.FindAsync(p => p.IsOpen);

        if (openProjects is not null)
        {
            foreach (var project in openProjects.OrderBy(p => p.CurrentTabIndex))
            {
                var file = new FilePath(project.FilePath);

                if (!file.Exists)
                {
                    // Remove from database
                    await liteDbContext.InferenceProjects.DeleteAsync(project.Id);
                }

                try
                {
                    if (file.Exists)
                    {
                        await AddTabFromFile(project.FilePath);
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(e, "Failed to open project file {FilePath}", project.FilePath);

                    notificationService.Show(
                        "Failed to open project file",
                        $"[{e.GetType().Name}] {e.Message}",
                        NotificationType.Error
                    );

                    // Set not open
                    await liteDbContext.InferenceProjects.UpdateAsync(
                        project with
                        {
                            IsOpen = false,
                            IsSelected = false,
                            CurrentTabIndex = -1
                        }
                    );
                }
            }
        }

        if (Tabs.Count == 0)
        {
            AddTab(InferenceProjectType.TextToImage);
        }
    }

    /// <summary>
    /// On exit, sync tab states to database
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await SyncTabStatesWithDatabase();

        foreach (var scope in scopes)
        {
            scope.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Update the database with current tabs
    /// </summary>
    private async Task SyncTabStatesWithDatabase()
    {
        // Update the database with the current tabs
        foreach (var (i, tab) in Tabs.ToImmutableArray().Enumerate())
        {
            if (tab.ProjectFile is not { } projectFile)
            {
                continue;
            }

            var projectPath = projectFile.ToString();

            var entry = await liteDbContext.InferenceProjects.FindOneAsync(p => p.FilePath == projectPath);

            // Create if not found
            entry ??= new InferenceProjectEntry { Id = Guid.NewGuid(), FilePath = projectFile.ToString() };

            entry.IsOpen = tab == SelectedTab;
            entry.CurrentTabIndex = i;

            Logger.Trace(
                "SyncTabStatesWithDatabase updated entry for tab '{Title}': {@Entry}",
                tab.TabTitle,
                entry
            );
            await liteDbContext.InferenceProjects.UpsertAsync(entry);
        }
    }

    /// <summary>
    /// Update the database with given tab
    /// </summary>
    private async Task SyncTabStateWithDatabase(InferenceTabViewModelBase tab)
    {
        if (tab.ProjectFile is not { } projectFile)
        {
            return;
        }

        var entry = await liteDbContext.InferenceProjects.FindOneAsync(
            p => p.FilePath == projectFile.ToString()
        );

        // Create if not found
        entry ??= new InferenceProjectEntry { Id = Guid.NewGuid(), FilePath = projectFile.ToString() };

        entry.IsOpen = tab == SelectedTab;
        entry.CurrentTabIndex = Tabs.IndexOf(tab);

        Logger.Trace(
            "SyncTabStatesWithDatabase updated entry for tab '{Title}': {@Entry}",
            tab.TabTitle,
            entry
        );
        await liteDbContext.InferenceProjects.UpsertAsync(entry);
    }

    /// <summary>
    /// When the + button on the tab control is clicked, add a new tab.
    /// </summary>
    [RelayCommand]
    private void AddTab(InferenceProjectType type)
    {
        if (type.ToViewModelType() is not { } vmType)
        {
            return;
        }

        // Create a new scope for this tab
        var scope = scopeFactory.CreateScope();
        scopes.Add(scope);

        // Register a TabContext in this scope
        var tabContext = new TabContext();
        scope.ServiceProvider.GetRequiredService<IServiceCollection>().AddScoped(_ => tabContext);

        // Get the view model using the scope's service provider
        var tab =
            scope.ServiceProvider.GetService(vmType) as InferenceTabViewModelBase
            ?? throw new NullReferenceException($"Could not create view model of type {vmType}");

        Tabs.Add(tab);

        // Set as new selected tab
        SelectedTabIndex = Tabs.Count - 1;

        // Update the database with the current tab
        SyncTabStateWithDatabase(tab).SafeFireAndForget();
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

        // Set the selected tab to the next tab if there is one, then previous, then null
        lock (Tabs)
        {
            var index = Tabs.IndexOf(vm);
            if (index < Tabs.Count - 1)
            {
                SelectedTabIndex = index + 1;
            }
            else if (index > 0)
            {
                SelectedTabIndex = index - 1;
            }

            // Remove the tab
            Tabs.RemoveAt(index);

            // Dispose the scope for this tab
            if (index < scopes.Count)
            {
                scopes[index].Dispose();
                scopes.RemoveAt(index);
            }
        }

        // Update the database with the current tab
        SyncTabStateWithDatabase(vm).SafeFireAndForget();

        // Dispose the view model
        vm.Dispose();
    }

    /// <summary>
    /// Show the connection help dialog.
    /// </summary>
    [RelayCommand]
    private async Task ShowConnectionHelp()
    {
        var vm = vmFactory.Get<InferenceConnectionHelpViewModel>();
        var result = await vm.CreateDialog().ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        selectedPackageId = vm.SelectedPackage?.Id;
    }

    /// <summary>
    /// Connect to the inference server.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Connect(CancellationToken cancellationToken = default)
    {
        if (ClientManager.IsConnected)
            return;

        if (Design.IsDesignMode)
        {
            await ClientManager.ConnectAsync(cancellationToken);
            return;
        }

        if (RunningPackage is not null)
        {
            var result = await notificationService.TryAsync(
                ClientManager.ConnectAsync(RunningPackage, cancellationToken),
                "Could not connect to backend"
            );

            if (result.Exception is { } exception)
            {
                Logger.Error(exception, "Failed to connect to Inference backend");
            }
        }
    }

    /// <summary>
    /// Disconnect from the inference server.
    /// </summary>
    [RelayCommand]
    private async Task Disconnect()
    {
        if (!ClientManager.IsConnected)
            return;

        if (Design.IsDesignMode)
        {
            await ClientManager.CloseAsync();
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
            stream.SetLength(0); // Overwrite fully

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
                NotificationType.Error
            );
            return;
        }

        // Update project file
        currentTab.ProjectFile = new FilePath(result.TryGetLocalPath()!);

        await SyncTabStatesWithDatabase();

        notificationService.Show("Saved", $"Saved project to {result.Name}", NotificationType.Success);
    }

    /// <summary>
    /// Menu "Save Project" command.
    /// </summary>
    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task MenuSave()
    {
        if (SelectedTab is not { } currentTab)
        {
            Logger.Info("MenuSaveProject: currentTab is null");
            return;
        }

        // If the tab has no project file, prompt for save as
        if (currentTab.ProjectFile is not { } projectFile)
        {
            await MenuSaveAs();
            return;
        }

        // Otherwise, save to the current project file
        var document = InferenceProjectDocument.FromLoadable(currentTab);

        // Save to file
        try
        {
            await using var stream = projectFile.Info.OpenWrite();
            stream.SetLength(0); // Overwrite fully

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
                NotificationType.Error
            );
            return;
        }

        notificationService.Show("Saved", $"Saved project to {projectFile.Name}", NotificationType.Success);
    }

    [RelayCommand]
    private void GoForwardTabWithLooping()
    {
        if (SelectedTabIndex == Tabs.Count - 1)
        {
            SelectedTabIndex = 0;
        }
        else
        {
            SelectedTabIndex++;
        }
    }

    [RelayCommand]
    private void GoBackwardsTabWithLooping()
    {
        if (SelectedTabIndex == 0)
        {
            SelectedTabIndex = Tabs.Count - 1;
        }
        else
        {
            SelectedTabIndex--;
        }
    }

    private async Task AddTabFromFile(FilePath file)
    {
        await using var stream = file.Info.OpenRead();

        var document = await JsonSerializer.DeserializeAsync<InferenceProjectDocument>(stream);
        if (document is null)
        {
            throw new ApplicationException("MenuOpenProject: Deserialize project file returned null");
        }

        if (document.State is null)
        {
            throw new ApplicationException("Project file does not have 'State' key");
        }

        document.VerifyVersion();

        if (document.ProjectType.ToViewModelType() is not { } vmType)
        {
            throw new InvalidOperationException($"Unsupported project type: {document.ProjectType}");
        }

        // Create a new scope for this tab
        var scope = scopeFactory.CreateScope();
        scopes.Add(scope);

        // Register a TabContext in this scope
        var tabContext = new TabContext();
        scope.ServiceProvider.GetRequiredService<IServiceCollection>().AddScoped(_ => tabContext);

        // Get the view model using the scope's service provider
        var vm =
            scope.ServiceProvider.GetService(vmType) as InferenceTabViewModelBase
            ?? throw new NullReferenceException($"Could not create view model of type {vmType}");

        vm.LoadStateFromJsonObject(document.State);
        vm.ProjectFile = file;

        Tabs.Add(vm);

        SelectedTab = vm;

        await SyncTabStatesWithDatabase();
    }

    private async Task AddTabFromFileAsync(LocalImageFile imageFile, InferenceProjectType projectType)
    {
        // Create a new scope for this tab
        var scope = scopeFactory.CreateScope();
        scopes.Add(scope);

        // Register a TabContext in this scope
        var tabContext = new TabContext();
        scope.ServiceProvider.GetRequiredService<IServiceCollection>().AddScoped(_ => tabContext);

        // Get the appropriate view model from the scope
        InferenceTabViewModelBase vm = projectType switch
        {
            InferenceProjectType.TextToImage
                => scope.ServiceProvider.GetRequiredService<InferenceTextToImageViewModel>(),
            InferenceProjectType.ImageToImage
                => scope.ServiceProvider.GetRequiredService<InferenceImageToImageViewModel>(),
            InferenceProjectType.ImageToVideo
                => scope.ServiceProvider.GetRequiredService<InferenceImageToVideoViewModel>(),
            InferenceProjectType.Upscale
                => scope.ServiceProvider.GetRequiredService<InferenceImageUpscaleViewModel>(),
            InferenceProjectType.FluxTextToImage
                => scope.ServiceProvider.GetRequiredService<InferenceFluxTextToImageViewModel>(),
        };

        switch (vm)
        {
            case InferenceImageToImageViewModel imgToImgVm:
                imgToImgVm.SelectImageCardViewModel.ImageSource = new ImageSource(imageFile.AbsolutePath);
                vm.LoadImageMetadata(imageFile.AbsolutePath);
                break;
            case InferenceTextToImageViewModel _:
                vm.LoadImageMetadata(imageFile.AbsolutePath);
                break;
            case InferenceImageUpscaleViewModel upscaleVm:
                upscaleVm.IsUpscaleEnabled = true;
                upscaleVm.SelectImageCardViewModel.ImageSource = new ImageSource(imageFile.AbsolutePath);
                break;
            case InferenceImageToVideoViewModel imgToVidVm:
                imgToVidVm.SelectImageCardViewModel.ImageSource = new ImageSource(imageFile.AbsolutePath);
                break;
            case InferenceFluxTextToImageViewModel _:
                vm.LoadImageMetadata(imageFile.AbsolutePath);
                break;
        }

        Tabs.Add(vm);
        SelectedTab = vm;

        await SyncTabStatesWithDatabase();
    }

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
        var file = results[0].TryGetLocalPath()!;

        try
        {
            await AddTabFromFile(file);
        }
        catch (NotSupportedException e)
        {
            notificationService.ShowPersistent(
                $"Unsupported Project Version",
                $"[{Path.GetFileName(file)}] {e.Message}",
                NotificationType.Error
            );
        }
        catch (Exception e)
        {
            notificationService.ShowPersistent(
                $"Failed to load Project",
                $"[{Path.GetFileName(file)}] {e.Message}",
                NotificationType.Error
            );
        }
    }
}
