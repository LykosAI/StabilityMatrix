using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using StabilityMatrix.Models.Packages;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;

namespace StabilityMatrix.ViewModels;

internal class InstallerViewModel : INotifyPropertyChanged
{
    private string installedText;
    private int progressValue;
    private bool isIndeterminate;
    private BasePackage selectedPackage;

    public InstallerViewModel()
    {
        InstalledText = "shrug";
        ProgressValue = 0;
    }

    public Task OnLoaded()
    {
        SelectedPackage = Packages.First();
        return Task.CompletedTask;
    }
    
    public static ObservableCollection<BasePackage> Packages => new()
    {
        new A3WebUI(),
        new DankDiffusion()
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

    public bool IsIndeterminate
    {
        get => isIndeterminate;
        set
        {
            if (value == isIndeterminate) return;
            isIndeterminate = value;
            OnPropertyChanged();
        }
    }

    public BasePackage SelectedPackage
    {
        get => selectedPackage;
        set
        {
            selectedPackage = value;
            OnPropertyChanged();
        }
    }

    public Visibility ProgressBarVisibility => ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;

    public RelayCommand InstallCommand => new(() => Debug.WriteLine(SelectedPackage.GithubUrl));
    private async Task<bool> InstallGitIfNecessary()
    {
        var gitOutput = await ProcessRunner.GetProcessOutputAsync("git", "--version");
        if (gitOutput.Contains("git version 2"))
        {
            return true;
        }

        IsIndeterminate = true;
        InstalledText = "Installing Git...";
        using var installProcess = ProcessRunner.StartProcess("Assets\\Git-2.40.1-64-bit.exe", "/VERYSILENT /NORESTART");
        await installProcess.WaitForExitAsync();
        IsIndeterminate = false;

        return installProcess.ExitCode == 0;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
