using StabilityMatrix.Helper;
using EventManager = StabilityMatrix.Core.Helper.EventManager;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;

namespace StabilityMatrix.ViewModels;

public partial class LaunchViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IContentDialogService contentDialogService;
    private readonly LaunchOptionsDialogViewModel launchOptionsDialogViewModel;
    private readonly ILogger<LaunchViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly IDialogFactory dialogFactory;
    private readonly ISnackbarService snackbarService;
    private readonly ISharedFolders sharedFolders;

    private BasePackage? runningPackage;
    private bool clearingPackages;
    private string webUiUrl = string.Empty;

    [ObservableProperty]
    private string consoleInput = "";

    [ObservableProperty]
    private Visibility launchButtonVisibility;

    [ObservableProperty]
    private Visibility stopButtonVisibility;

    [ObservableProperty]
    private bool isLaunchTeachingTipsOpen = false;

    [ObservableProperty]
    private bool showWebUiButton;

    [ObservableProperty]
    private ObservableCollection<string>? consoleHistory;

    [ObservableProperty]
    private InstalledPackage? selectedPackage;

    [ObservableProperty]
    private ObservableCollection<InstalledPackage> installedPackages = new();

    public LaunchViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IContentDialogService contentDialogService,
        LaunchOptionsDialogViewModel launchOptionsDialogViewModel,
        ILogger<LaunchViewModel> logger,
        IPyRunner pyRunner,
        IDialogFactory dialogFactory,
        ISnackbarService snackbarService,
        ISharedFolders sharedFolders
    )
    {
        this.pyRunner = pyRunner;
        this.dialogFactory = dialogFactory;
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.snackbarService = snackbarService;
        this.sharedFolders = sharedFolders;
        SetProcessRunning(false);

        EventManager.Instance.InstalledPackagesChanged += OnInstalledPackagesChanged;
        EventManager.Instance.TeachingTooltipNeeded += OnTeachingTooltipNeeded;

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompatOnOnActivated;
    }

    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        if (clearingPackages)
            return;
        settingsManager.Transaction(s => s.ActiveInstalledPackageId = value?.Id);
    }

    private void OnTeachingTooltipNeeded(object? sender, EventArgs e)
    {
        IsLaunchTeachingTipsOpen = true;
    }

    private void OnInstalledPackagesChanged(object? sender, EventArgs e)
    {
        OnLoaded();
    }

    private void ToastNotificationManagerCompatOnOnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        if (!e.Argument.StartsWith("http"))
            return;

        if (string.IsNullOrWhiteSpace(webUiUrl))
        {
            webUiUrl = e.Argument;
        }

        LaunchWebUi();
    }

    public AsyncRelayCommand LaunchCommand =>
        new(async () =>
        {
            var activeInstall = SelectedPackage;

            if (activeInstall == null)
            {
                // No selected package: error snackbar
                snackbarService
                    .ShowSnackbarAsync(
                        "You must install and select a package before launching",
                        "No package selected"
                    )
                    .SafeFireAndForget();
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
                    activeInstallName
                );
                snackbarService
                    .ShowSnackbarAsync(
                        "Install package name did not match a definition. Please reinstall and let us know about this issue.",
                        "Package name invalid"
                    )
                    .SafeFireAndForget();
                return;
            }

            // If this is the first launch (LaunchArgs is null),
            // load and save a launch options dialog in background
            // so that dynamic initial values are saved.
            if (activeInstall.LaunchArgs == null)
            {
                var definitions = basePackage.LaunchOptions;
                // Open a config page and save it
                var dialog = dialogFactory.CreateLaunchOptionsDialog(definitions, activeInstall);
                var args = dialog.AsLaunchArgs();
                settingsManager.SaveLaunchArgs(activeInstall.Id, args);
            }

            // Clear console
            ConsoleHistory?.Clear();

            await pyRunner.Initialize();

            // Get path from package
            var packagePath = $"{settingsManager.LibraryDir}\\{activeInstall.LibraryPath!}";

            basePackage.ConsoleOutput += OnConsoleOutput;
            basePackage.Exited += OnExit;
            basePackage.StartupComplete += RunningPackageOnStartupComplete;

            // Update shared folder links (in case library paths changed)
            await sharedFolders.UpdateLinksForPackage(basePackage, packagePath);

            // Load user launch args from settings and convert to string
            var userArgs = settingsManager.GetLaunchArgs(activeInstall.Id);
            var userArgsString = string.Join(" ", userArgs.Select(opt => opt.ToArgString()));

            // Join with extras, if any
            userArgsString = string.Join(" ", userArgsString, basePackage.ExtraLaunchArguments);
            await basePackage.RunPackage(packagePath, userArgsString);
            runningPackage = basePackage;
            SetProcessRunning(true);
        });

    [RelayCommand]
    private async Task ConfigAsync()
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
        if (package is IArgParsable parsable)
        {
            var rootPath = activeInstall.FullPath!;
            var moduleName = parsable.RelativeArgsDefinitionScriptPath;
            var parser = new ArgParser(pyRunner, rootPath, moduleName);
            definitions = await parser.GetArgsAsync();
        }

        // Open a config page
        var dialog = dialogFactory.CreateLaunchOptionsDialog(definitions, activeInstall);
        dialog.IsPrimaryButtonEnabled = true;
        dialog.PrimaryButtonText = "Save";
        dialog.CloseButtonText = "Cancel";
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Save config
            var args = dialog.AsLaunchArgs();
            settingsManager.SaveLaunchArgs(activeInstall.Id, args);
        }
    }

    [RelayCommand]
    private void LaunchWebUi()
    {
        ProcessRunner.OpenUrl(webUiUrl);
    }

    private void RunningPackageOnStartupComplete(object? sender, string url)
    {
        webUiUrl = url;
        ShowWebUiButton = true;
        // Uwp toasts has upstream issues on some machines
        // https://github.com/CommunityToolkit/WindowsCommunityToolkit/issues/4858
        try
        {
            new ToastContentBuilder()
                .AddText("Stable Diffusion Web UI ready to go!")
                .AddButton("Open Web UI", ToastActivationType.Foreground, url)
                .Show();
        }
        catch (Exception e)
        {
            logger.LogWarning("Failed to show Windows notification: {Message}", e.Message);
        }
    }

    public void OnLoaded()
    {
        LoadPackages();
        lock (InstalledPackages)
        {
            // Skip if no packages
            if (!InstalledPackages.Any())
            {
                logger.LogTrace($"No packages for {nameof(LaunchViewModel)}");
                return;
            }

            var activePackageId = settingsManager.Settings.ActiveInstalledPackageId;
            if (activePackageId != null)
            {
                SelectedPackage =
                    InstalledPackages.FirstOrDefault(x => x.Id == activePackageId) ?? InstalledPackages[0];
            }
        }
    }

    public void OnShutdown()
    {
        Stop();
    }

    [RelayCommand]
    private Task Stop()
    {
        if (runningPackage != null)
        {
            runningPackage.StartupComplete -= RunningPackageOnStartupComplete;
            runningPackage.ConsoleOutput -= OnConsoleOutput;
            runningPackage.Exited -= OnExit;
        }

        runningPackage?.Shutdown();
        runningPackage = null;
        SetProcessRunning(false);
        ConsoleHistory?.Add($"Stopped process at {DateTimeOffset.Now}");
        ShowWebUiButton = false;
        return Task.CompletedTask;
    }

    private void LoadPackages()
    {
        var packages = settingsManager.Settings.InstalledPackages;
        if (!packages?.Any() ?? true)
        {
            InstalledPackages.Clear();
            return;
        }

        clearingPackages = true;
        InstalledPackages.Clear();

        foreach (var package in packages)
        {
            InstalledPackages.Add(package);
        }

        clearingPackages = false;
    }

    private void SetProcessRunning(bool running)
    {
        if (running)
        {
            LaunchButtonVisibility = Visibility.Collapsed;
            StopButtonVisibility = Visibility.Visible;
        }
        else
        {
            LaunchButtonVisibility = Visibility.Visible;
            StopButtonVisibility = Visibility.Collapsed;
        }
    }

    private void OnConsoleOutput(object? sender, ProcessOutput output)
    {
        if (string.IsNullOrWhiteSpace(output.Text))
            return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            ConsoleHistory ??= new ObservableCollection<string>();

            if (output.Text.Contains("Total progress") && !output.Text.Contains(" 0%"))
            {
                ConsoleHistory[^2] = output.Text.TrimEnd('\n');
            }
            else if (
                (output.Text.Contains("it/s") || output.Text.Contains("s/it") || output.Text.Contains("B/s"))
                && !output.Text.Contains("Total progress")
                && !output.Text.Contains(" 0%")
            )
            {
                ConsoleHistory[^1] = output.Text.TrimEnd('\n');
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(output.Text.TrimEnd('\n')))
                {
                    ConsoleHistory.Add(output.Text.TrimEnd('\n'));
                }
            }

            ConsoleHistory = new ObservableCollection<string>(
                ConsoleHistory.Where(str => !string.IsNullOrWhiteSpace(str))
            );
        });
    }

    private void OnExit(object? sender, int exitCode)
    {
        var basePackage = packageFactory.FindPackageByName(SelectedPackage.PackageName);
        basePackage.ConsoleOutput -= OnConsoleOutput;
        basePackage.Exited -= OnExit;
        basePackage.StartupComplete -= RunningPackageOnStartupComplete;

        Application.Current.Dispatcher.Invoke(() =>
        {
            ConsoleHistory?.Add($"Venv process exited with code {exitCode}");
            SetProcessRunning(false);
            ShowWebUiButton = false;
        });
    }
}
