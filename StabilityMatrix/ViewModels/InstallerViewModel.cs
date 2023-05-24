using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace StabilityMatrix.ViewModels;

internal class InstallerViewModel : INotifyPropertyChanged
{
    private string installedText;
    private int progressValue;
    private bool isIndeterminate;

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

    public Visibility ProgressBarVisibility => ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;

    public AsyncRelayCommand InstallCommand => new(InstallGitIfNecessary);
    private async Task InstallGitIfNecessary()
    {
        var gitOutput = await ProcessRunner.GetProcessOutputAsync("git", "--version");
        if (gitOutput.Contains("git version 2"))
        {
            InstalledText = "Installed";
            IsIndeterminate = false;
            ProgressValue = 100;
            return;
        }

        InstalledText = "Not Installed";
        IsIndeterminate = true;
        using var installProcess = ProcessRunner.StartProcess("Assets\\Git-2.40.1-64-bit.exe", "/VERYSILENT /NORESTART");
        await installProcess.WaitForExitAsync();

        if (installProcess.ExitCode == 0)
        {
            InstalledText = "Git successfully installed";
            ProgressValue = 100;
        }
        else
        {
            InstalledText = $"There was an error installing Git: {installProcess.ExitCode}";
            ProgressValue = 0;
        }
        IsIndeterminate = false;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
