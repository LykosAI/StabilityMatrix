using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.Notifications;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class LaunchViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private BasePackage? runningPackage;
    private bool clearingPackages = false;

    [ObservableProperty]
    private string consoleInput = "";

    [ObservableProperty]
    private string consoleOutput = "";

    [ObservableProperty]
    private Visibility launchButtonVisibility;

    [ObservableProperty]
    private Visibility stopButtonVisibility;


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

    [ObservableProperty]
    private ObservableCollection<InstalledPackage> installedPackages = new();

    public event EventHandler? ScrollNeeded;

    public LaunchViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        SetProcessRunning(false);
        
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompatOnOnActivated;
    }

    private void ToastNotificationManagerCompatOnOnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        if (e.Argument.StartsWith("http"))
        {
            Process.Start(new ProcessStartInfo(e.Argument) { UseShellExecute = true });
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

        await PyRunner.Initialize();

        // Get path from package
        var packagePath = SelectedPackage.Path!;
        var basePackage = PackageFactory.FindPackageByName(SelectedPackage.Name!);
        if (basePackage == null)
        {
            throw new InvalidOperationException("Package not found");
        }
        
        basePackage.ConsoleOutput += OnConsoleOutput;
        basePackage.Exited += OnExit;
        basePackage.StartupComplete += RunningPackageOnStartupComplete;
        await basePackage.RunPackage(packagePath, string.Empty);
        runningPackage = basePackage;
        SetProcessRunning(true);
    });

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
        if (InstalledPackages.Any() && settingsManager.Settings.ActiveInstalledPackage != null)
        {
            SelectedPackage =
                InstalledPackages[
                    InstalledPackages.IndexOf(InstalledPackages.FirstOrDefault(x =>
                        x.Id == settingsManager.Settings.ActiveInstalledPackage))];
        }
        else if (InstalledPackages.Any())
        {
            SelectedPackage = InstalledPackages[0];
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
