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
using StabilityMatrix.Helper;
using StabilityMatrix.Models;

namespace StabilityMatrix.ViewModels;

public partial class SelectInstallLocationsViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private const string ValidExistingDirectoryText = "Valid existing data directory found";
    private const string InvalidDirectoryText =
        "Directory must be empty or have a valid settings.json file";
    
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
    
    public SelectInstallLocationsViewModel()
    {
        DataDirectory = DefaultInstallLocation;
        
        RefreshBadgeViewModel.RefreshFunc = ValidateDataDirectory;
    }

    public void OnLoaded()
    {
        RefreshBadgeViewModel.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }
    
    // Revalidate on data directory change
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDataDirectoryChanged(string value)
    {
        RefreshBadgeViewModel.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }
    
    // Validates current data directory
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
        // Check if the directory is empty
        var isEmpty = !Directory.EnumerateFileSystemEntries(DataDirectory).Any();
        // If not, show error badge, and set directory to invalid to prevent continuing
        if (!isEmpty)
        {
            IsStatusBadgeVisible = true;
            IsDirectoryValid = false;
            DirectoryStatusText = InvalidDirectoryText;
            return false;
        }
        // Otherwise, hide badge and set directory to valid
        IsStatusBadgeVisible = false;
        IsDirectoryValid = true;
        return true;
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
