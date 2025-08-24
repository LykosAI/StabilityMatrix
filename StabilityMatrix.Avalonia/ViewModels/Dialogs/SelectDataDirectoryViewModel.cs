using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(SelectDataDirectoryDialog))]
[ManagedService]
[RegisterTransient<SelectDataDirectoryViewModel>]
public partial class SelectDataDirectoryViewModel : ContentDialogViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string DefaultInstallLocation =>
        Compat.IsLinux
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "StabilityMatrix"
            )
            : Compat.AppDataHome;

    private readonly ISettingsManager settingsManager;

    private const string ValidExistingDirectoryText = "Valid existing data directory found";
    private const string InvalidDirectoryText = "Directory must be empty or have a valid settings.json file";
    private const string NotEnoughFreeSpaceText = "Not enough free space on the selected drive";
    private const string FatWarningText = "FAT32 / exFAT drives are not supported at this time";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInTempFolder))]
    private string dataDirectory = DefaultInstallLocation;

    [ObservableProperty]
    private bool isPortableMode;

    [ObservableProperty]
    private string directoryStatusText = string.Empty;

    [ObservableProperty]
    private bool isStatusBadgeVisible;

    [ObservableProperty]
    private bool isDirectoryValid;

    [ObservableProperty]
    private bool showFatWarning;

    public bool IsInTempFolder =>
        Compat
            .AppCurrentDir.ToString()
            .StartsWith(
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase
            );

    public RefreshBadgeViewModel ValidatorRefreshBadge { get; } =
        new()
        {
            State = ProgressState.Inactive,
            SuccessToolTipText = ValidExistingDirectoryText,
            FailToolTipText = InvalidDirectoryText,
        };

    public SelectDataDirectoryViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        ValidatorRefreshBadge.RefreshFunc = ValidateDataDirectory;
    }

    public override void OnLoaded()
    {
        ValidatorRefreshBadge.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
        IsPortableMode = true;
    }

    // Revalidate on data directory change
    partial void OnDataDirectoryChanged(string value)
    {
        ValidatorRefreshBadge.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }

    private async Task<bool> ValidateDataDirectory()
    {
        await using var delay = new MinimumDelay(100, 200);

        ShowFatWarning = IsDriveFat(DataDirectory);

        if (IsInTempFolder)
            return false;

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
                var _ = JsonSerializer.Deserialize<Core.Models.Settings.Settings>(
                    jsonText,
                    new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
                );
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

    private bool CanPickFolder => App.StorageProvider.CanPickFolder;

    [RelayCommand(CanExecute = nameof(CanPickFolder))]
    private async Task ShowFolderBrowserDialog()
    {
        var provider = App.StorageProvider;
        var result = await provider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select Data Folder", AllowMultiple = false }
        );

        if (result.Count != 1)
            return;

        DataDirectory = result[0].Path.LocalPath;
    }

    partial void OnIsPortableModeChanged(bool value)
    {
        DataDirectory = value ? Compat.AppCurrentDir + "Data" : DefaultInstallLocation;
    }

    private bool IsDriveFat(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path));
            return drive.DriveFormat.Contains("FAT", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Error checking drive FATness");
            return false;
        }
    }
}
