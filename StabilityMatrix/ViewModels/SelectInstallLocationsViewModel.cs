using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using Ookii.Dialogs.Wpf;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.ViewModels;

public partial class SelectInstallLocationsViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private const string ValidExistingDirectoryText = "Valid existing data directory found";
    private const string InvalidDirectoryText =
        "Directory must be empty or have a valid settings.json file";
    private const string NotEnoughFreeSpaceText = "Not enough free space on the selected drive";
    
    [ObservableProperty] private string dataDirectory;
    [ObservableProperty] private bool isPortableMode;
    
    [ObservableProperty] private string directoryStatusText = string.Empty;
    [ObservableProperty] private bool isStatusBadgeVisible;
    [ObservableProperty] private bool isDirectoryValid;

    public RefreshBadgeViewModel RefreshBadgeViewModel { get; } = new()
    {
        State = ProgressState.Inactive,
        SuccessToolTipText = ValidExistingDirectoryText,
        FailToolTipText = InvalidDirectoryText
    };
    
    public string DefaultInstallLocation => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix");

    public bool HasOldData => settingsManager.GetOldInstalledPackages().Any();
    
    public SelectInstallLocationsViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        DataDirectory = DefaultInstallLocation;
        RefreshBadgeViewModel.RefreshFunc = ValidateDataDirectory;
    }

    public void OnLoaded()
    {
        RefreshBadgeViewModel.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }
    
    // Revalidate on data directory change
    partial void OnDataDirectoryChanged(string value)
    {
        RefreshBadgeViewModel.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }
    
    private async Task<bool> ValidateDataDirectory()
    {
        await using var delay = new MinimumDelay(100, 200);

        // Doesn't exist, this is fine as a new install, hide badge
        if (!Directory.Exists(DataDirectory))
        {
            IsStatusBadgeVisible = false;
            IsDirectoryValid = true;
            return true;
        }
        // Otherwise check that a settings.json exists
        var settingsPath = Path.Combine(DataDirectory, "settings.json");
        
        // settings.json exists: Try deserializing it
        if (File.Exists(settingsPath))
        {
            try
            {
                var jsonText = await File.ReadAllTextAsync(settingsPath);
                var _ = JsonSerializer.Deserialize<Settings>(jsonText, new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                });
                // If successful, show existing badge
                IsStatusBadgeVisible = true;
                IsDirectoryValid = true;
                DirectoryStatusText = ValidExistingDirectoryText;
                return true;
            }
            catch (Exception e)
            {
                Logger.Info("Failed to deserialize settings.json: {Msg}", e.Message);
                // If not, show error badge, and set directory to invalid to prevent continuing
                IsStatusBadgeVisible = true;
                IsDirectoryValid = false;
                DirectoryStatusText = InvalidDirectoryText;
                return false;
            }
        }
        
        // No settings.json
        
        // Check if the directory is %APPDATA%\StabilityMatrix: hide badge and set directory valid
        if (DataDirectory == DefaultInstallLocation)
        {
            IsStatusBadgeVisible = false;
            IsDirectoryValid = true;
            return true;
        }
        
        // Check if the directory is empty: hide badge and set directory to valid
        var isEmpty = !Directory.EnumerateFileSystemEntries(DataDirectory).Any();
        if (isEmpty)
        {
            IsStatusBadgeVisible = false;
            IsDirectoryValid = true;
            return true;
        }

        // Not empty and not appdata: show error badge, and set directory to invalid
        IsStatusBadgeVisible = true;
        IsDirectoryValid = false;
        DirectoryStatusText = InvalidDirectoryText;
        return false;
    }

    [RelayCommand]
    private void ShowFolderBrowserDialog()
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select a folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.SelectedPath;
        if (path == null) return;
        
        DataDirectory = path;
    }

    partial void OnIsPortableModeChanged(bool value)
    {
        DataDirectory = value
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data")
                : DefaultInstallLocation;
    }
}
