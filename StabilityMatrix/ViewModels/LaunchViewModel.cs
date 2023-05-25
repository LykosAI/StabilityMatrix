using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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

    public ObservableCollection<InstalledPackage> Packages => new();

    public LaunchViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        Packages.CollectionChanged += PackagesOnCollectionChanged;
    }

    private void PackagesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        
        var newPackage = e.NewItems?.Cast<InstalledPackage>().FirstOrDefault();
        if (newPackage != null)
        {
            settingsManager.AddInstalledPackage(newPackage);
        }
    }

    public RelayCommand LaunchCommand => new(() =>
    {
        ConsoleOutput = "";
        
        var venv = new PyVenvRunner(@"L:\Image ML\stable-diffusion-webui\venv");
        
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

        const string arg = "\"" + @"L:\Image ML\stable-diffusion-webui\launch.py" + "\"";
        // const string arg = "-c \"import sys; print(sys.version_info)\"";
        
        venv.RunDetached(arg, onConsoleOutput, onExit);
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
            Packages.Add(package);
        }
    }
}
