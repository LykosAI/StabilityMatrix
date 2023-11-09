using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
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
using NLog;
using StabilityMatrix.Avalonia.Extensions;
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
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using InferenceTabViewModelBase = StabilityMatrix.Avalonia.ViewModels.Base.InferenceTabViewModelBase;
using Path = System.IO.Path;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[Preload]
[View(typeof(InferencePage))]
[Singleton]
public partial class InferenceViewModel : PageViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly INotificationService notificationService;

    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IModelIndexService modelIndexService;
    private readonly ILiteDbContext liteDbContext;

    private bool isFirstLoadComplete;

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

    public InferenceViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ISettingsManager settingsManager,
        IModelIndexService modelIndexService,
        ILiteDbContext liteDbContext,
        SharedState sharedState
    )
    {
        this.vmFactory = vmFactory;
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.modelIndexService = modelIndexService;
        this.liteDbContext = liteDbContext;

        ClientManager = inferenceClientManager;
        SharedState = sharedState;

        // Keep RunningPackage updated with the current package pair
        EventManager.Instance.RunningPackageStatusChanged += OnRunningPackageStatusChanged;

        // "Send to Inference"
        EventManager.Instance.InferenceTextToImageRequested += OnInferenceTextToImageRequested;
        EventManager.Instance.InferenceUpscaleRequested += OnInferenceUpscaleRequested;

        MenuSaveAsCommand.WithConditionalNotificationErrorHandler(notificationService);
        MenuOpenProjectCommand.WithConditionalNotificationErrorHandler(notificationService);
    }

    /// <summary>
    /// Updates the RunningPackage property when the running package changes.
    /// Also starts a connection to the backend if a new ComfyUI package is running.
    /// And disconnects if the package is closed.
    /// </summary>
    private void OnRunningPackageStatusChanged(
        object? sender,
        RunningPackageStatusChangedEventArgs e
    )
    {
        RunningPackage = e.CurrentPackagePair;

        IDisposable? onStartupComplete = null;

        Dispatcher.UIThread.Post(() =>
        {
            if (e.CurrentPackagePair?.BasePackage is ComfyUI package)
            {
                IsWaitingForConnection = true;
                onStartupComplete = Observable
                    .FromEventPattern<string>(package, nameof(package.StartupComplete))
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
            else
            {
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
        });
    }

    public override void OnLoaded()
    {
        if (!Design.IsDesignMode && !isFirstLoadComplete)
        {
            isFirstLoadComplete = true;
            OnInitialLoad().SafeFireAndForget();
        }

        modelIndexService.BackgroundRefreshIndex();
    }

    private async Task OnInitialLoad()
    {
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

    private void OnInferenceTextToImageRequested(object? sender, LocalImageFile e)
    {
        Dispatcher.UIThread.Post(() => AddTabFromImage(e).SafeFireAndForget());
    }

    private void OnInferenceUpscaleRequested(object? sender, LocalImageFile e)
    {
        Dispatcher.UIThread.Post(() => AddUpscalerTabFromImage(e).SafeFireAndForget());
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

            var entry = await liteDbContext.InferenceProjects.FindOneAsync(
                p => p.FilePath == projectPath
            );

            // Create if not found
            entry ??= new InferenceProjectEntry
            {
                Id = Guid.NewGuid(),
                FilePath = projectFile.ToString()
            };

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
        entry ??= new InferenceProjectEntry
        {
            Id = Guid.NewGuid(),
            FilePath = projectFile.ToString()
        };

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

        var tab =
            vmFactory.Get(vmType) as InferenceTabViewModelBase
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
        await vm.CreateDialog().ShowAsync();
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
            await notificationService.TryAsync(
                ClientManager.ConnectAsync(RunningPackage, cancellationToken),
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

        notificationService.Show(
            "Saved",
            $"Saved project to {result.Name}",
            NotificationType.Success
        );
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

        notificationService.Show(
            "Saved",
            $"Saved project to {projectFile.Name}",
            NotificationType.Success
        );
    }

    private async Task AddTabFromFile(FilePath file)
    {
        await using var stream = file.Info.OpenRead();

        var document = await JsonSerializer.DeserializeAsync<InferenceProjectDocument>(stream);
        if (document is null)
        {
            throw new ApplicationException(
                "MenuOpenProject: Deserialize project file returned null"
            );
        }

        if (document.State is null)
        {
            throw new ApplicationException("Project file does not have 'State' key");
        }

        document.VerifyVersion();

        InferenceTabViewModelBase vm;
        if (document.ProjectType is InferenceProjectType.TextToImage)
        {
            // Get view model
            var textToImage = vmFactory.Get<InferenceTextToImageViewModel>();
            // Load state
            textToImage.LoadStateFromJsonObject(document.State);
            // Set the file backing the view model
            textToImage.ProjectFile = file;
            vm = textToImage;
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported project type: {document.ProjectType}"
            );
        }

        Tabs.Add(vm);

        SelectedTab = vm;

        await SyncTabStatesWithDatabase();
    }

    private async Task AddTabFromImage(LocalImageFile imageFile)
    {
        var metadata = imageFile.ReadMetadata();
        InferenceTabViewModelBase? vm = null;

        if (!string.IsNullOrWhiteSpace(metadata.SMProject))
        {
            var document = JsonSerializer.Deserialize<InferenceProjectDocument>(metadata.SMProject);
            if (document is null)
            {
                throw new ApplicationException(
                    "MenuOpenProject: Deserialize project file returned null"
                );
            }

            if (document.State is null)
            {
                throw new ApplicationException("Project file does not have 'State' key");
            }

            document.VerifyVersion();
            var textToImage = vmFactory.Get<InferenceTextToImageViewModel>();
            textToImage.LoadStateFromJsonObject(document.State);
            vm = textToImage;
        }
        else if (!string.IsNullOrWhiteSpace(metadata.Parameters))
        {
            if (GenerationParameters.TryParse(metadata.Parameters, out var generationParameters))
            {
                var textToImageViewModel = vmFactory.Get<InferenceTextToImageViewModel>();
                textToImageViewModel.LoadStateFromParameters(generationParameters);
                vm = textToImageViewModel;
            }
        }

        if (vm == null)
        {
            notificationService.Show(
                "Unable to load project from image",
                "No image metadata found",
                NotificationType.Error
            );
            return;
        }

        Tabs.Add(vm);

        SelectedTab = vm;

        await SyncTabStatesWithDatabase();
    }

    private async Task AddUpscalerTabFromImage(LocalImageFile imageFile)
    {
        var upscaleVm = vmFactory.Get<InferenceImageUpscaleViewModel>();
        upscaleVm.IsUpscaleEnabled = true;
        upscaleVm.SelectImageCardViewModel.ImageSource = new ImageSource(imageFile.AbsolutePath);

        Tabs.Add(upscaleVm);
        SelectedTab = upscaleVm;

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
