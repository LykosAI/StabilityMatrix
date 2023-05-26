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

    public event EventHandler ScrollNeeded;

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
        var packagePath = SelectedPackage.Path;
        var venvPath = Path.Combine(packagePath, "venv");

        // Setup venv
        venvRunner?.Dispose();
        venvRunner = new PyVenvRunner(venvPath);
        if (!venvRunner.Exists())
        {
            await venvRunner.Setup();
        }

        void OnConsoleOutput(string? s)
        {
            if (s == null) return;
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Debug.WriteLine($"process stdout: {s}");
                ConsoleOutput += s + "\n";
                ScrollNeeded?.Invoke(this, EventArgs.Empty);
            });
        }

        void OnExit(int i)
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                Debug.WriteLine($"Venv process exited with code {i}");
                ConsoleOutput += $"Venv process exited with code {i}";
                ScrollNeeded?.Invoke(this, EventArgs.Empty);
                SetProcessRunning(false);
            });
        }

        var args = "\"" + Path.Combine(packagePath, SelectedPackage.LaunchCommand) + "\"";

        venvRunner.RunDetached(args, OnConsoleOutput, OnExit);
        SetProcessRunning(true);
    });

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
        venvRunner?.Dispose();
    }

    [RelayCommand]
    private async Task Stop()
    {
        venvRunner?.Dispose();
        if (venvRunner?.Process != null)
        {
            await venvRunner.Process.WaitForExitAsync();
        }
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
}
