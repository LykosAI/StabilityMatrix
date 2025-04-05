using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(LaunchPageView))]
public partial class LaunchPageViewModel : PageViewModelBase, IDisposable, IAsyncDisposable
{
    private readonly ILogger<LaunchPageViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPyRunner pyRunner;
    private readonly INotificationService notificationService;
    private readonly ISharedFolders sharedFolders;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    protected readonly IPackageFactory PackageFactory;

    // Regex to match if input contains a yes/no prompt,
    // i.e "Y/n", "yes/no". Case insensitive.
    // Separated by / or |.
    [GeneratedRegex(@"y(/|\|)n|yes(/|\|)no", RegexOptions.IgnoreCase)]
    private static partial Regex InputYesNoRegex();

    public override string Title => "Launch";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Rocket, IconVariant = IconVariant.Filled };

    public ConsoleViewModel Console { get; } = new();

    [ObservableProperty]
    private bool launchButtonVisibility;

    [ObservableProperty]
    private bool stopButtonVisibility;

    [ObservableProperty]
    private bool isLaunchTeachingTipsOpen;

    [ObservableProperty]
    private bool showWebUiButton;

    [
        ObservableProperty,
        NotifyPropertyChangedFor(nameof(SelectedBasePackage), nameof(SelectedPackageExtraCommands))
    ]
    private InstalledPackage? selectedPackage;

    [ObservableProperty]
    private ObservableCollection<InstalledPackage> installedPackages = new();

    [ObservableProperty]
    private PackagePair? runningPackage;

    [ObservableProperty]
    private bool autoScrollToEnd = true;

    public virtual BasePackage? SelectedBasePackage =>
        PackageFactory.FindPackageByName(SelectedPackage?.PackageName);

    public IEnumerable<string> SelectedPackageExtraCommands =>
        SelectedBasePackage?.ExtraLaunchCommands ?? Enumerable.Empty<string>();

    // private bool clearingPackages;
    private string webUiUrl = string.Empty;

    // Input info-bars
    [ObservableProperty]
    private bool showManualInputPrompt;

    [ObservableProperty]
    private bool showConfirmInputPrompt;

    public LaunchPageViewModel(
        ILogger<LaunchPageViewModel> logger,
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IPyRunner pyRunner,
        INotificationService notificationService,
        ISharedFolders sharedFolders,
        ServiceManager<ViewModelBase> dialogFactory
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.PackageFactory = packageFactory;
        this.pyRunner = pyRunner;
        this.notificationService = notificationService;
        this.sharedFolders = sharedFolders;
        this.dialogFactory = dialogFactory;

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SelectedPackage,
            settings => settings.ActiveInstalledPackage
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.AutoScrollToEnd,
            settings => settings.AutoScrollLaunchConsoleToEnd
        );

        EventManager.Instance.PackageLaunchRequested += OnPackageLaunchRequested;
        EventManager.Instance.OneClickInstallFinished += OnOneClickInstallFinished;
        EventManager.Instance.InstalledPackagesChanged += OnInstalledPackagesChanged;
        EventManager.Instance.TeachingTooltipNeeded += OnTeachingTooltipNeeded;
        // Handler for console input
        Console.ApcInput += (_, message) =>
        {
            if (InputYesNoRegex().IsMatch(message.Data))
            {
                ShowConfirmInputPrompt = true;
            }
            else
            {
                ShowManualInputPrompt = true;
            }
        };
    }

    private void OnTeachingTooltipNeeded(object? sender, EventArgs e) => IsLaunchTeachingTipsOpen = true;

    private void OnInstalledPackagesChanged(object? sender, EventArgs e) => OnLoaded();

    private void OnPackageLaunchRequested(object? sender, Guid e)
    {
        if (RunningPackage is not null)
        {
            notificationService.Show(
                "A package is already running",
                "Please stop the current package before launching another.",
                NotificationType.Error
            );
            return;
        }

        SelectedPackage = InstalledPackages.FirstOrDefault(x => x.Id == e);
        LaunchAsync().SafeFireAndForget();
    }

    partial void OnAutoScrollToEndChanged(bool value)
    {
        if (value)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }
    }

    protected override Task OnInitialLoadedAsync()
    {
        if (string.IsNullOrWhiteSpace(Program.Args.LaunchPackageName))
            return base.OnInitialLoadedAsync();

        var package = InstalledPackages.FirstOrDefault(x => x.DisplayName == Program.Args.LaunchPackageName);
        if (package is not null)
        {
            SelectedPackage = package;
            return LaunchAsync();
        }

        package = InstalledPackages.FirstOrDefault(x => x.Id.ToString() == Program.Args.LaunchPackageName);
        if (package is null)
            return base.OnInitialLoadedAsync();

        SelectedPackage = package;
        return LaunchAsync();
    }

    public override void OnLoaded()
    {
        // Load installed packages
        InstalledPackages = new ObservableCollection<InstalledPackage>(
            settingsManager.Settings.InstalledPackages
        );

        // Ensure active package either exists or is null
        if (SelectedPackage?.Id is { } id && InstalledPackages.All(x => x.Id != id))
        {
            settingsManager.Transaction(
                s =>
                {
                    s.UpdateActiveInstalledPackage();
                },
                ignoreMissingLibraryDir: true
            );
        }

        // Load active package
        SelectedPackage = settingsManager.Settings.ActiveInstalledPackage;
        AutoScrollToEnd = settingsManager.Settings.AutoScrollLaunchConsoleToEnd;

        base.OnLoaded();
    }

    [RelayCommand]
    public async Task LaunchAsync(string? command = null)
    {
        await notificationService.TryAsync(LaunchImpl(command));
    }

    protected virtual async Task LaunchImpl(string? command)
    {
        IsLaunchTeachingTipsOpen = false;

        var activeInstall = SelectedPackage;
        if (activeInstall == null)
        {
            // No selected package: error notification
            notificationService.Show(
                new Notification(
                    message: "You must install and select a package before launching",
                    title: "No package selected",
                    type: NotificationType.Error
                )
            );
            return;
        }

        var activeInstallName = activeInstall.PackageName;
        var basePackage = string.IsNullOrWhiteSpace(activeInstallName)
            ? null
            : PackageFactory.FindPackageByName(activeInstallName);

        if (basePackage == null)
        {
            logger.LogWarning(
                "During launch, package name '{PackageName}' did not match a definition",
                activeInstallName
            );

            notificationService.Show(
                new Notification(
                    "Package name invalid",
                    "Install package name did not match a definition. Please reinstall and let us know about this issue.",
                    NotificationType.Error
                )
            );
            return;
        }

        // If this is the first launch (LaunchArgs is null),
        // load and save a launch options dialog vm
        // so that dynamic initial values are saved.
        if (activeInstall.LaunchArgs == null)
        {
            var definitions = basePackage.LaunchOptions;
            // Create config cards and save them
            var cards = LaunchOptionCard
                .FromDefinitions(definitions, Array.Empty<LaunchOption>())
                .ToImmutableArray();

            var args = cards.SelectMany(c => c.Options).ToList();

            logger.LogDebug(
                "Setting initial launch args: {Args}",
                string.Join(", ", args.Select(o => o.ToArgString()?.ToRepr()))
            );

            settingsManager.SaveLaunchArgs(activeInstall.Id, args);
        }

        if (basePackage is not StableSwarm)
        {
            await pyRunner.Initialize();
        }

        // Get path from package
        var packagePath = new DirectoryPath(settingsManager.LibraryDir, activeInstall.LibraryPath!);

        if (basePackage is not StableSwarm)
        {
            // Unpack sitecustomize.py to venv
            await UnpackSiteCustomize(packagePath.JoinDir("venv"));
        }

        basePackage.Exited += OnProcessExited;
        basePackage.StartupComplete += RunningPackageOnStartupComplete;

        // Clear console and start update processing
        await Console.StopUpdatesAsync();
        await Console.Clear();
        Console.StartUpdates();

        // Update shared folder links (in case library paths changed)
        await basePackage.UpdateModelFolders(
            packagePath,
            activeInstall.PreferredSharedFolderMethod ?? basePackage.RecommendedSharedFolderMethod
        );

        // Load user launch args from settings and convert to string
        var userArgs = activeInstall.LaunchArgs ?? [];
        var userArgsString = string.Join(" ", userArgs.Select(opt => opt.ToArgString()));

        // Join with extras, if any
        userArgsString = string.Join(" ", userArgsString, basePackage.ExtraLaunchArguments);

        // Use input command if provided, otherwise use package launch command
        command ??= basePackage.LaunchCommand;

        // await basePackage.RunPackage(packagePath, command, userArgsString, OnProcessOutputReceived);
        RunningPackage = new PackagePair(activeInstall, basePackage);

        EventManager.Instance.OnRunningPackageStatusChanged(RunningPackage);
    }

    // Unpacks sitecustomize.py to the target venv
    private static async Task UnpackSiteCustomize(DirectoryPath venvPath)
    {
        var sitePackages = venvPath.JoinDir(PyVenvRunner.RelativeSitePackagesPath);
        var file = sitePackages.JoinFile("sitecustomize.py");
        file.Directory?.Create();
        await Assets.PyScriptSiteCustomize.ExtractTo(file, true);
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

        var package = PackageFactory.FindPackageByName(name);
        if (package == null)
        {
            logger.LogWarning("Package {Name} not found", name);
            return;
        }

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
        var viewModel = dialogFactory.Get<LaunchOptionsViewModel>();
        viewModel.Cards = LaunchOptionCard
            .FromDefinitions(package.LaunchOptions, activeInstall.LaunchArgs ?? [])
            .ToImmutableArray();

        logger.LogDebug("Launching config dialog with cards: {CardsCount}", viewModel.Cards.Count);

        var dialog = new BetterContentDialog
        {
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsPrimaryButtonEnabled = true,
            PrimaryButtonText = Resources.Action_Save,
            CloseButtonText = Resources.Action_Cancel,
            FullSizeDesired = true,
            DefaultButton = ContentDialogButton.Primary,
            ContentMargin = new Thickness(32, 16),
            Padding = new Thickness(0, 16),
            Content = new LaunchOptionsDialog { DataContext = viewModel, }
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Save config
            var args = viewModel.AsLaunchArgs();
            settingsManager.SaveLaunchArgs(activeInstall.Id, args);
        }
    }

    // Send user input to running package
    public async Task SendInput(string input)
    {
        if (RunningPackage?.BasePackage is BaseGitPackage gitPackage)
        {
            var venv = gitPackage.VenvRunner;
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
            Console.Post("y\n");
            await SendInput("y\n");
        }
        else
        {
            Console.Post("n\n");
            await SendInput("n\n");
        }

        ShowConfirmInputPrompt = false;
    }

    [RelayCommand]
    private async Task SendManualInput(string input)
    {
        // Also send input to our own console
        Console.PostLine(input);
        await SendInput(input);
    }

    public virtual async Task Stop()
    {
        if (RunningPackage is null)
            return;
        await RunningPackage.BasePackage.WaitForShutdown();
        RunningPackage = null;
        ShowWebUiButton = false;

        Console.PostLine($"{Environment.NewLine}Stopped process at {DateTimeOffset.Now}");
    }

    public void OpenWebUi()
    {
        if (string.IsNullOrEmpty(webUiUrl))
            return;

        notificationService.TryAsync(
            Task.Run(() => ProcessRunner.OpenUrl(webUiUrl)),
            "Failed to open URL",
            $"{webUiUrl}"
        );
    }

    private void OnProcessExited(object? sender, int exitCode)
    {
        EventManager.Instance.OnRunningPackageStatusChanged(null);
        Dispatcher
            .UIThread.InvokeAsync(async () =>
            {
                logger.LogTrace("Process exited ({Code}) at {Time:g}", exitCode, DateTimeOffset.Now);

                // Need to wait for streams to finish before detaching handlers
                if (sender is BaseGitPackage { VenvRunner: not null } package)
                {
                    var process = package.VenvRunner.Process;
                    if (process is not null)
                    {
                        // Max 5 seconds
                        var ct = new CancellationTokenSource(5000).Token;
                        try
                        {
                            await process.WaitUntilOutputEOF(ct);
                        }
                        catch (OperationCanceledException e)
                        {
                            logger.LogWarning("Waiting for process EOF timed out: {Message}", e.Message);
                        }
                    }
                }

                // Detach handlers
                if (sender is BasePackage basePackage)
                {
                    basePackage.Exited -= OnProcessExited;
                    basePackage.StartupComplete -= RunningPackageOnStartupComplete;
                }
                RunningPackage = null;
                ShowWebUiButton = false;

                await Console.StopUpdatesAsync();

                // Need to reset cursor in case its in some weird position
                // from progress bars
                await Console.ResetWriteCursor();
                Console.PostLine($"{Environment.NewLine}Process finished with exit code {exitCode}");
            })
            .SafeFireAndForget();
    }

    // Callback for processes
    private void OnProcessOutputReceived(ProcessOutput output)
    {
        Console.Post(output);

        if (AutoScrollToEnd)
        {
            EventManager.Instance.OnScrollToBottomRequested();
        }
    }

    private void OnOneClickInstallFinished(object? sender, bool e)
    {
        OnLoaded();
    }

    private void RunningPackageOnStartupComplete(object? sender, string e)
    {
        webUiUrl = e.Replace("0.0.0.0", "127.0.0.1");
        ShowWebUiButton = !string.IsNullOrWhiteSpace(webUiUrl);
    }

    public void OnMainWindowClosing(WindowClosingEventArgs e)
    {
        if (RunningPackage != null)
        {
            // Show confirmation
            if (e.CloseReason is WindowCloseReason.WindowClosing)
            {
                e.Cancel = true;

                var dialog = CreateExitConfirmDialog();
                Dispatcher
                    .UIThread.InvokeAsync(async () =>
                    {
                        if (
                            (TaskDialogStandardResult)await dialog.ShowAsync(true)
                            == TaskDialogStandardResult.Yes
                        )
                        {
                            App.Services.GetRequiredService<MainWindow>().Hide();
                            App.Shutdown();
                        }
                    })
                    .SafeFireAndForget();
            }
        }
    }

    private static TaskDialog CreateExitConfirmDialog()
    {
        var dialog = DialogHelper.CreateTaskDialog(
            "Confirm Exit",
            "Are you sure you want to exit? This will also close the currently running package."
        );

        dialog.ShowProgressBar = false;
        dialog.FooterVisibility = TaskDialogFooterVisibility.Never;

        dialog.Buttons = new List<TaskDialogButton>
        {
            new("Exit", TaskDialogStandardResult.Yes),
            TaskDialogButton.CancelButton
        };
        dialog.Buttons[0].IsDefault = true;

        return dialog;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RunningPackage?.BasePackage.Shutdown();
            RunningPackage = null;

            Console.Dispose();
        }

        base.Dispose(disposing);
    }

    public async ValueTask DisposeAsync()
    {
        if (RunningPackage is not null)
        {
            await RunningPackage.BasePackage.WaitForShutdown();
            RunningPackage = null;
        }
        await Console.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
