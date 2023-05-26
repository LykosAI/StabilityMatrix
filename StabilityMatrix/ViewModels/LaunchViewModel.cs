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
    private PyVenvRunner? venvRunner;
    
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
            settingsManager.SetActiveInstalledPackage(value);
            OnPropertyChanged();
        }
    }

    public ObservableCollection<InstalledPackage> InstalledPackages = new();
    
    public event EventHandler ScrollNeeded;

    public LaunchViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
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
        var packagePath = SelectedPackage.Path;
        var venvPath = Path.Combine(packagePath, "venv");
        
        // Setup venv
        venvRunner?.Dispose();
        venvRunner = new PyVenvRunner(venvPath);
        if (!venvRunner.Exists())
        {
            await venvRunner.Setup();
        }
        
        var onConsoleOutput = new Action<string?>(s =>
        {
            if (s == null) return;
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Debug.WriteLine($"process stdout: {s}");
                ConsoleOutput += s + "\n";
                ScrollNeeded?.Invoke(this, EventArgs.Empty);
            });
        });
        
        var onExit = new Action<int>(i =>
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Debug.WriteLine($"Venv process exited with code {i}");
                ConsoleOutput += $"Venv process exited with code {i}";
                ScrollNeeded?.Invoke(this, EventArgs.Empty);
                SetProcessRunning(false);
            });
        });

        var args = "\"" + Path.Combine(packagePath, "launch.py") + "\"";

        venvRunner.RunDetached(args, onConsoleOutput, onExit);
        SetProcessRunning(true);
    });

    public void OnLoaded()
    {
        SetProcessRunning(false);
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
        venvRunner?.Dispose();
    }

    [RelayCommand]
    private void Stop()
    {
        venvRunner?.Dispose();
        venvRunner = null;
        SetProcessRunning(false);
        ConsoleOutput += $"{Environment.NewLine}Stopped process at {DateTimeOffset.Now}{Environment.NewLine}";
    }

    private void LoadPackages()
    {
        var packages = settingsManager.Settings.InstalledPackages;
        if (!packages.Any())
        {
            return;
        }
        
        InstalledPackages.Clear();
        
        foreach (var package in packages)
        {
            InstalledPackages.Add(package);
        }
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
}
