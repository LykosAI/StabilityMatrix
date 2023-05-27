using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using Wpf.Ui.Appearance;

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

    private void RunningPackageOnStartupComplete(object? sender, EventArgs e)
    {
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "http://localhost:7860",
            UseShellExecute = true
        });
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
