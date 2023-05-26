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
    
    [ObservableProperty]
    public string consoleInput = "";

    [ObservableProperty]
    public string consoleOutput = "";
    
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
        
        // Get path from package
        var packagePath = SelectedPackage.Path;
        var venvPath = Path.Combine(packagePath, "venv");
        
        // Setup venv
        var venv = new PyVenvRunner(venvPath);
        if (!venv.Exists())
        {
            await venv.Setup();
        }
        
        var onConsoleOutput = new Action<string?>(s =>
        {
            if (s == null) return;
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Debug.WriteLine($"process stdout: {s}");
                ConsoleOutput += s + "\n";
            });
        });
        
        var onExit = new Action<int>(i =>
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Debug.WriteLine($"Venv process exited with code {i}");
                ConsoleOutput += $"Venv process exited with code {i}";
            });
        });

        var args = "\"" + Path.Combine(packagePath, "launch.py") + "\"";

        venv.RunDetached(args, onConsoleOutput, onExit);
    });

    public void OnLoaded()
    {
        LoadPackages();
    }

    private void LoadPackages()
    {
        var packages = settingsManager.Settings.InstalledPackages;
        if (!packages.Any())
        {
            return;
        }
        
        foreach (var package in packages)
        {
            InstalledPackages.Clear();
            InstalledPackages.Add(package);
        }
    }
}
