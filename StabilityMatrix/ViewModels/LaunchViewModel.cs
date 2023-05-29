using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

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

    private BasePackage? runningPackage;
    private bool clearingPackages = false;

    [ObservableProperty] private string consoleInput = "";

    [ObservableProperty] private string consoleOutput = "";

    [ObservableProperty] private Visibility launchButtonVisibility;

    [ObservableProperty] private Visibility stopButtonVisibility;


    private InstalledPackage? selectedPackage;

    public InstalledPackage? SelectedPackage
    {
        get => selectedPackage;
        set
        {
            if (value == selectedPackage) return;
            selectedPackage = value;

            if (!clearingPackages)
            {
                settingsManager.SetActiveInstalledPackage(value);
            }

            OnPropertyChanged();
        }
    }

    [ObservableProperty] private ObservableCollection<InstalledPackage> installedPackages = new();

    public event EventHandler? ScrollNeeded;

    public LaunchViewModel(ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IContentDialogService contentDialogService,
        LaunchOptionsDialogViewModel launchOptionsDialogViewModel,
        ILogger<LaunchViewModel> logger,
        IPyRunner pyRunner,
        IDialogFactory dialogFactory)
    {
        this.pyRunner = pyRunner;
        this.dialogFactory = dialogFactory;
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        SetProcessRunning(false);

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompatOnOnActivated;
    }

    private void ToastNotificationManagerCompatOnOnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        if (e.Argument.StartsWith("http"))
        {
            Process.Start(new ProcessStartInfo(e.Argument) {UseShellExecute = true});
        }
    }

    public AsyncRelayCommand LaunchCommand => new(async () =>
    {
        // Clear console
        ConsoleOutput = "";

        if (SelectedPackage == null)
        {
            ConsoleOutput = "No package selected";
            return;
        }

        await pyRunner.Initialize();

        // Get path from package
        var packagePath = SelectedPackage.Path!;
        var basePackage = packageFactory.FindPackageByName(SelectedPackage.PackageName!);
        if (basePackage == null)
        {
            throw new InvalidOperationException("Package not found");
        }

        basePackage.ConsoleOutput += OnConsoleOutput;
        basePackage.Exited += OnExit;
        basePackage.StartupComplete += RunningPackageOnStartupComplete;
        var userArgs = string.Join(" ", settingsManager.GetLaunchArgs(SelectedPackage.Id));
        await basePackage.RunPackage(packagePath, userArgs);
        runningPackage = basePackage;
        SetProcessRunning(true);
    });

    [RelayCommand]
    public async Task ConfigAsync()
    {
        var activeInstall = SelectedPackage;
        var name = activeInstall?.Name;
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

        // Open a config page
        var dialog = dialogFactory.CreateLaunchOptionsDialog(package, activeInstall);
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

    private void RunningPackageOnStartupComplete(object? sender, string url)
    {
        new ToastContentBuilder()
            .AddText("Stable Diffusion Web UI ready to go!")
            .AddButton("Launch Web UI", ToastActivationType.Foreground, url)
            .Show();
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
            var activePackageId = settingsManager.Settings.ActiveInstalledPackage;
            if (activePackageId != null)
            {
                SelectedPackage = InstalledPackages.FirstOrDefault(
                    x => x.Id == activePackageId) ?? InstalledPackages[0];
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
        ConsoleOutput += $"{Environment.NewLine}Stopped process at {DateTimeOffset.Now}{Environment.NewLine}";
        return Task.CompletedTask;
    }

    private void LoadPackages()
    {
        var packages = settingsManager.Settings.InstalledPackages;
        if (!packages.Any())
        {
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

    private void OnConsoleOutput(object? sender, string output)
    {
        if (output == null) return;
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            ConsoleOutput += output + "\n";
            ScrollNeeded?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnExit(object? sender, int exitCode)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            ConsoleOutput += $"Venv process exited with code {exitCode}";
            ScrollNeeded?.Invoke(this, EventArgs.Empty);
            SetProcessRunning(false);
        });
    }
}
