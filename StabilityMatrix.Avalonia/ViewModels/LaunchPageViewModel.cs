using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(LaunchPageView))]
public partial class LaunchPageViewModel : PageViewModelBase, IDisposable
{
    private readonly ILogger<LaunchPageViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPyRunner pyRunner;
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    
    public override string Title => "Launch";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Rocket, IsFilled = true};

    [ObservableProperty]
    private TextDocument consoleDocument = new();
    
    // Queue for console updates
    private readonly BufferBlock<ProcessOutput> consoleUpdateBuffer = new();
    // Task that updates the console (runs on UI thread)
    private Task? consoleUpdateTask;
    private CancellationTokenSource? consoleUpdateCts;
    
    [ObservableProperty] private bool launchButtonVisibility;
    [ObservableProperty] private bool stopButtonVisibility;
    [ObservableProperty] private bool isLaunchTeachingTipsOpen;
    [ObservableProperty] private bool showWebUiButton;
    
    [ObservableProperty] private InstalledPackage? selectedPackage;
    [ObservableProperty] private ObservableCollection<InstalledPackage> installedPackages = new();

    [ObservableProperty] private BasePackage? runningPackage;
    
    // private bool clearingPackages;
    private string webUiUrl = string.Empty;
    
    // Input info-bars
    [ObservableProperty] private bool showManualInputPrompt;
    [ObservableProperty] private bool showConfirmInputPrompt;

    public LaunchPageViewModel(ILogger<LaunchPageViewModel> logger, ISettingsManager settingsManager, IPackageFactory packageFactory,
        IPyRunner pyRunner, INotificationService notificationService, ServiceManager<ViewModelBase> dialogFactory)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.pyRunner = pyRunner;
        this.notificationService = notificationService;
        this.dialogFactory = dialogFactory;

        EventManager.Instance.PackageLaunchRequested +=
            async (s, e) => await OnPackageLaunchRequested(s, e);
        EventManager.Instance.OneClickInstallFinished += OnOneClickInstallFinished;
    }

    private async Task OnPackageLaunchRequested(object? sender, Guid e)
    {
        OnLoaded();
        SelectedPackage = InstalledPackages.FirstOrDefault(x => x.Id == e);
        if (SelectedPackage is null) return;
        
        await LaunchAsync();
    }

    public override void OnLoaded()
    {
        // Ensure active package either exists or is null
        settingsManager.Transaction(s => s.UpdateActiveInstalledPackage());
        
        // Load installed packages
        InstalledPackages =
            new ObservableCollection<InstalledPackage>(settingsManager.Settings.InstalledPackages);
        
        // Load active package
        SelectedPackage = settingsManager.Settings.GetActiveInstalledPackage();
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        var activeInstall = SelectedPackage;

        if (activeInstall == null)
        {
            // No selected package: error notification
            notificationService.Show(new Notification(
                message: "You must install and select a package before launching",
                title: "No package selected",
                type: NotificationType.Error));
            return;
        }

        var activeInstallName = activeInstall.PackageName;
        var basePackage = string.IsNullOrWhiteSpace(activeInstallName)
            ? null
            : packageFactory.FindPackageByName(activeInstallName);

        if (basePackage == null)
        {
            logger.LogWarning(
                "During launch, package name '{PackageName}' did not match a definition",
                activeInstallName);
            
            notificationService.Show(new Notification("Package name invalid",
                "Install package name did not match a definition. Please reinstall and let us know about this issue.",
                NotificationType.Error));
            return;
        }

        // If this is the first launch (LaunchArgs is null),
        // load and save a launch options dialog vm
        // so that dynamic initial values are saved.
        if (activeInstall.LaunchArgs == null)
        {
            var definitions = basePackage.LaunchOptions;
            // Open a config page and save it
            var viewModel = dialogFactory.Get<LaunchOptionsViewModel>();
            
            viewModel.Cards = LaunchOptionCard
                .FromDefinitions(definitions, Array.Empty<LaunchOption>())
                .ToImmutableArray();
            
            var args = viewModel.AsLaunchArgs();   
            
            logger.LogDebug("Setting initial launch args: {Args}", 
                string.Join(", ", args.Select(o => o.ToArgString()?.ToRepr())));
     
            settingsManager.SaveLaunchArgs(activeInstall.Id, args);
        }

        // Clear console
        ConsoleDocument.Text = string.Empty;

        await pyRunner.Initialize();

        // Get path from package
        var packagePath = Path.Combine(settingsManager.LibraryDir, activeInstall.LibraryPath!);

        basePackage.ConsoleOutput += OnProcessOutputReceived;
        basePackage.Exited += OnProcessExited;
        basePackage.StartupComplete += RunningPackageOnStartupComplete;
        
        // Start task to update console from queue
        consoleUpdateCts = new CancellationTokenSource();
        // Run in UI thread
        consoleUpdateTask = Dispatcher.UIThread.InvokeAsync(async () 
                => await BeginUpdateConsole(consoleUpdateCts.Token), DispatcherPriority.Render);

        // Update shared folder links (in case library paths changed)
        //sharedFolders.UpdateLinksForPackage(basePackage, packagePath);

        // Load user launch args from settings and convert to string
        var userArgs = settingsManager.GetLaunchArgs(activeInstall.Id);
        var userArgsString = string.Join(" ", userArgs.Select(opt => opt.ToArgString()));

        // Join with extras, if any
        userArgsString = string.Join(" ", userArgsString, basePackage.ExtraLaunchArguments);
        await basePackage.RunPackage(packagePath, userArgsString);
        RunningPackage = basePackage;
    }
    
    [RelayCommand]
    private async Task Config()
    {
        var activeInstall = SelectedPackage;
        var name = activeInstall?.PackageName;
        if (name == null || activeInstall == null)
        {
            logger.LogWarning($"Selected package is null");
            return;
        }

        var package = packageFactory.FindPackageByName(name);
        if (package == null)
        {
            logger.LogWarning("Package {Name} not found", name);
            return;
        }

        var definitions = package.LaunchOptions;
        // Check if package supports IArgParsable
        // Use dynamic parsed args over static
        /*if (package is IArgParsable parsable)
        {
            var rootPath = activeInstall.FullPath!;
            var moduleName = parsable.RelativeArgsDefinitionScriptPath;
            var parser = new ArgParser(pyRunner, rootPath, moduleName);
            definitions = await parser.GetArgsAsync();
        }*/

        // Open a config page
        var userLaunchArgs = settingsManager.GetLaunchArgs(activeInstall.Id);
        var viewModel = dialogFactory.Get<LaunchOptionsViewModel>();
        viewModel.Cards = LaunchOptionCard.FromDefinitions(definitions, userLaunchArgs)
            .ToImmutableArray();
        
        logger.LogDebug("Launching config dialog with cards: {CardsCount}", 
            viewModel.Cards.Count);
        
        var dialog = new BetterContentDialog
        {
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsPrimaryButtonEnabled = true,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Padding = new Thickness(0, 16),
            Content = new LaunchOptionsDialog
            {
                DataContext = viewModel,
            }
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Save config
            var args = viewModel.AsLaunchArgs();
            settingsManager.SaveLaunchArgs(activeInstall.Id, args);
        }
    }
    
    private async Task BeginUpdateConsole(CancellationToken ct)
    {
        // This should be run in the UI thread
        Dispatcher.UIThread.CheckAccess();
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var output = await consoleUpdateBuffer.ReceiveAsync(ct);
                // Check for Apc messages
                if (output.ApcMessage is not null)
                {
                    // Handle Apc message, for now just input audit events
                    var message = output.ApcMessage.Value;
                    if (message.Type == ApcType.Input)
                    {
                        ShowConfirmInputPrompt = true;
                    }
                    // Ignore further processing
                    continue;
                }
                
                using var update = ConsoleDocument.RunUpdate();
                // Handle remove
                if (output.ClearLines > 0)
                {
                    for (var i = 0; i < output.ClearLines; i++)
                    {
                        var lastLineIndex = ConsoleDocument.LineCount - 1;
                        var line = ConsoleDocument.Lines[lastLineIndex];
                        ConsoleDocument.Remove(line.Offset, line.Length);
                    }
                }
                // Add new line
                ConsoleDocument.Insert(ConsoleDocument.TextLength, output.Text);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
    
    // Send user input to running package
    public async Task SendInput(string input)
    {
        if (RunningPackage is BaseGitPackage package)
        {
            var venv = package.VenvRunner;
            var process = venv?.Process;
            if (process is not null)
            {
                await process.StandardInput.WriteLineAsync(input);
            }
            else
            {
                logger.LogWarning("Attempted to write input but Process is null");
            }
        }
    }

    [RelayCommand]
    private async Task SendConfirmInput(bool value)
    {
        // This must be on the UI thread
        Dispatcher.UIThread.CheckAccess();
        // Also send input to our own console
        if (value)
        {
            consoleUpdateBuffer.Post(new ProcessOutput { Text = "y\n" });
            await SendInput("y\n");
        }
        else
        {
            consoleUpdateBuffer.Post(new ProcessOutput { Text = "n\n" });
            await SendInput("n\n");
        }

        ShowConfirmInputPrompt = false;
    }
    
    [RelayCommand]
    private async Task SendManualInput(string input)
    {
        // This must be on the UI thread
        Dispatcher.UIThread.CheckAccess();
        // Add newline
        input += Environment.NewLine;
        // Also send input to our own console
        consoleUpdateBuffer.Post(new ProcessOutput { Text = input });
        await SendInput(input);
    }
    
    public async Task Stop()
    {
        if (RunningPackage is null) return;
        await RunningPackage.Shutdown();
        
        RunningPackage = null;
        ShowWebUiButton = false;
        
        // Can't use buffer here since is null after shutdown
        Dispatcher.UIThread.Post(() =>
        {
            var msg = $"{Environment.NewLine}Stopped process at {DateTimeOffset.Now}{Environment.NewLine}";
            ConsoleDocument.Insert(ConsoleDocument.TextLength, msg );
        });
    }

    public void OpenWebUi()
    {
        if (string.IsNullOrEmpty(webUiUrl)) return;
        
        notificationService.TryAsync(Task.Run(() => ProcessRunner.OpenUrl(webUiUrl)),
        "Failed to open URL", $"{webUiUrl}");
    }
    
    private void OnProcessExited(object? sender, int exitCode)
    {
        if (sender is BasePackage package)
        {
            package.ConsoleOutput -= OnProcessOutputReceived;
            package.Exited -= OnProcessExited;
            package.StartupComplete -= RunningPackageOnStartupComplete;
        }
        RunningPackage = null;
        ShowWebUiButton = false;
        var msg =
            $"{Environment.NewLine}Process finished with exit code {exitCode}{Environment.NewLine}";
        
        // Add to buffer
        consoleUpdateBuffer.Post(new ProcessOutput { RawText = msg, Text = msg });
        
        // Task that waits until buffer is empty, then stops the console update task
        Task.Run(async () =>
        {
            Debug.WriteLine($"Waiting for console update buffer to empty ({consoleUpdateBuffer.Count})");
            while (consoleUpdateBuffer.Count > 0)
            {
                await Task.Delay(100);
            }
            Debug.WriteLine("Console update buffer empty, stopping console update task");
            consoleUpdateCts?.Cancel();
            consoleUpdateCts = null;
            consoleUpdateTask = null;
        });
    }

    // Callback for processes
    private void OnProcessOutputReceived(object? sender, ProcessOutput output)
    {
        consoleUpdateBuffer.Post(output);
        EventManager.Instance.OnScrollToBottomRequested();
    }

    private void OnOneClickInstallFinished(object? sender, bool e)
    {
        OnLoaded();
    }
    
    private void RunningPackageOnStartupComplete(object? sender, string e)
    {
        webUiUrl = e;
        ShowWebUiButton = !string.IsNullOrWhiteSpace(webUiUrl);
    }
    
    public void Dispose()
    {
        // Dispose updates
        consoleUpdateBuffer.Complete();
        consoleUpdateCts?.Cancel();
        consoleUpdateCts = null;
        consoleUpdateTask = null;
        
        RunningPackage?.Shutdown();
        GC.SuppressFinalize(this);
    }
}
