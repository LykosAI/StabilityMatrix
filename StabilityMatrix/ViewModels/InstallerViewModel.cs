using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Popups;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace StabilityMatrix.ViewModels;

internal class InstallerViewModel : INotifyPropertyChanged
{
    private string installedText;
    private int progressValue;

    public InstallerViewModel()
    {
        InstalledText = "shrug";
        ProgressValue = 0;
    }

    public static ObservableCollection<BasePackage> Packages => new()
    {
        new A3WebUI(),
    };

    public string InstalledText
    {
        get => installedText;
        private set
        {
            if (value == installedText) return;
            installedText = value;
            OnPropertyChanged();
        }
    }

    public int ProgressValue
    {
        get => progressValue;
        set
        {
            if (value == progressValue) return;
            progressValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressBarVisibility));
        }
    }

    public Visibility ProgressBarVisibility => ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;

    public AsyncRelayCommand InstallCommand => new(InstallGitIfNecessary);
    private async Task InstallGitIfNecessary()
    {
        var gitOutput = await ProcessRunner.RunProcessAsync("git", "--version");
        if (gitOutput.Contains("git version 2"))
        {
            InstalledText = "Installed";
            ProgressValue = 100;
            return;
        }

        InstalledText = "Not Installed";
        var installProcess = ProcessRunner.RunProcess("Assets\\Git-2.40.1-64-bit.exe", "/SILENT /NORESTART");
        while (!installProcess.StandardOutput.EndOfStream && !installProcess.HasExited)
        {
            Debug.WriteLine(await installProcess.StandardOutput.ReadLineAsync());
        }

        if (installProcess.ExitCode == 0)
        {
            InstalledText = "Git successfully installed";
            ProgressValue = 100;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
