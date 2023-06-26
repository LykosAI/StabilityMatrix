using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using NLog;
using StabilityMatrix.Models;
using StabilityMatrix.Python;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Helper;

public class SettingsManager : ISettingsManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly ReaderWriterLockSlim FileLock = new();

    private readonly string? originalEnvPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
    
    // Library properties
    public bool IsPortableMode { get; set; }
    public string LibraryDir { get; set; } = string.Empty;
    
    // Dynamic paths from library
    public string DatabasePath => Path.Combine(LibraryDir, "StabilityMatrix.db");
    private string SettingsPath => Path.Combine(LibraryDir, "settings.json");
    public string ModelsDirectory => Path.Combine(LibraryDir, "Models");

    public Settings Settings { get; private set; } = new();

    public event EventHandler<bool>? ModelBrowserNsfwEnabledChanged;

    /// <summary>
    /// Attempts to locate and set the library path
    /// Return true if found, false otherwise
    /// </summary>
    public bool TryFindLibrary()
    {
        // 1. Check portable mode
        var appDir = AppContext.BaseDirectory;
        IsPortableMode = File.Exists(Path.Combine(appDir, ".sm-portable"));
        if (IsPortableMode)
        {
            LibraryDir = Path.Combine(appDir, "Data");
            SetStaticLibraryPaths();
            return true;
        }
        
        // 2. Check %APPDATA%/StabilityMatrix/library.json
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var libraryJsonPath = Path.Combine(appDataDir, "StabilityMatrix", "library.json");
        if (File.Exists(libraryJsonPath))
        {
            try
            {
                var libraryJson = File.ReadAllText(libraryJsonPath);
                var library = JsonSerializer.Deserialize<LibrarySettings>(libraryJson);
                if (!string.IsNullOrWhiteSpace(library?.LibraryPath))
                {
                    LibraryDir = library.LibraryPath;
                    SetStaticLibraryPaths();
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to read library.json in AppData: {Message}", e.Message);
            }
        }
        return false;
    }

    // Set static classes requiring library path
    private void SetStaticLibraryPaths()
    {
        ArchiveHelper.HomeDir = LibraryDir;
        PyRunner.HomeDir = LibraryDir;
    }

    /// <summary>
    /// Save a new library path to %APPDATA%/StabilityMatrix/library.json
    /// </summary>
    public void SetLibraryPath(string path)
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var homeDir = Path.Combine(appDataDir, "StabilityMatrix");
        Directory.CreateDirectory(homeDir);
        var libraryJsonPath = Path.Combine(homeDir, "library.json");
        
        var library = new LibrarySettings { LibraryPath = path };
        var libraryJson = JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(libraryJsonPath, libraryJson);
    } 
    
    /// <summary>
    /// Enable and create settings files for portable mode
    /// Creates the ./Data directory and the `.sm-portable` marker file
    /// </summary>
    public void SetPortableMode()
    {
        // Get app directory
        var appDir = AppContext.BaseDirectory;
        // Create data directory
        var dataDir = Path.Combine(appDir, "Data");
        Directory.CreateDirectory(dataDir);
        // Create marker file
        File.Create(Path.Combine(dataDir, ".sm-portable")).Close();
    }

    /// <summary>
    /// Iterable of installed packages using the old absolute path format.
    /// Can be called with Any() to check if the user needs to migrate.
    /// </summary>
    public IEnumerable<InstalledPackage> GetOldInstalledPackages()
    {
        var installed = Settings.InstalledPackages;
        // Absolute paths are old formats requiring migration
        foreach (var package in installed.Where(package => Path.IsPathRooted(package.Path)))
        {
            yield return package;
        }
    }

    public void SetTheme(string theme)
    {
        Settings.Theme = theme;
        SaveSettings();
    }

    public void AddInstalledPackage(InstalledPackage p)
    {
        Settings.InstalledPackages.Add(p);
        SaveSettings();
    }

    public void RemoveInstalledPackage(InstalledPackage p)
    {
        Settings.InstalledPackages.Remove(p);
        SetActiveInstalledPackage(Settings.InstalledPackages.Any() ? Settings.InstalledPackages.First() : null);
    }

    public void SetActiveInstalledPackage(InstalledPackage? p)
    {
        Settings.ActiveInstalledPackage = p?.Id;
        SaveSettings();
    }
    
    public void SetNavExpanded(bool navExpanded)
    {
        Settings.IsNavExpanded = navExpanded;
        SaveSettings();
    }
    
    public void AddPathExtension(string pathExtension)
    {
        Settings.PathExtensions ??= new List<string>();
        Settings.PathExtensions.Add(pathExtension);
        SaveSettings();
    }

    public string GetPathExtensionsAsString()
    {
        return string.Join(";", Settings.PathExtensions ?? new List<string>());
    }

    /// <summary>
    /// Insert path extensions to the front of the PATH environment variable
    /// </summary>
    public void InsertPathExtensions()
    {
        if (Settings.PathExtensions == null) return;
        var toInsert = GetPathExtensionsAsString();
        // Append the original path, if any
        if (originalEnvPath != null)
        {
            toInsert += $";{originalEnvPath}";
        }
        Environment.SetEnvironmentVariable("PATH", toInsert, EnvironmentVariableTarget.Process);
    }

    public void UpdatePackageVersionNumber(Guid id, string? newVersion)
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Id == id);
        if (package == null || newVersion == null)
        {
            return;
        }

        package.PackageVersion = newVersion;

        package.DisplayVersion = string.IsNullOrWhiteSpace(package.InstalledBranch)
            ? newVersion
            : $"{package.InstalledBranch}@{newVersion[..7]}";

        SaveSettings();
    }
    
    public void SetLastUpdateCheck(InstalledPackage package)
    {
        Settings.InstalledPackages.First(p => p.DisplayName == package.DisplayName).LastUpdateCheck = package.LastUpdateCheck;
        SaveSettings();
    }
    
    public List<LaunchOption> GetLaunchArgs(Guid packageId)
    {
        var packageData = Settings.InstalledPackages.FirstOrDefault(x => x.Id == packageId);
        return packageData?.LaunchArgs ?? new();
    }
    
    public void SaveLaunchArgs(Guid packageId, List<LaunchOption> launchArgs)
    {
        var packageData = Settings.InstalledPackages.FirstOrDefault(x => x.Id == packageId);
        if (packageData == null)
        {
            return;
        }
        // Only save if not null or default
        var toSave = launchArgs.Where(opt => !opt.IsEmptyOrDefault()).ToList();

        packageData.LaunchArgs = toSave;
        SaveSettings();
    }

    public void SetWindowBackdropType(WindowBackdropType backdropType)
    {
        Settings.WindowBackdropType = backdropType;
        SaveSettings();
    }
    
    public void SetHasSeenWelcomeNotification(bool hasSeenWelcomeNotification)
    {
        Settings.HasSeenWelcomeNotification = hasSeenWelcomeNotification;
        SaveSettings();
    }
    
    public string? GetActivePackageHost()
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Id == Settings.ActiveInstalledPackage);
        if (package == null) return null;
        var hostOption = package.LaunchArgs?.FirstOrDefault(x => x.Name.ToLowerInvariant() == "host");
        if (hostOption?.OptionValue != null)
        {
            return hostOption.OptionValue as string;
        }
        return hostOption?.DefaultValue as string;
    }

    public string? GetActivePackagePort()
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Id == Settings.ActiveInstalledPackage);
        if (package == null) return null;
        var portOption = package.LaunchArgs?.FirstOrDefault(x => x.Name.ToLowerInvariant() == "port");
        if (portOption?.OptionValue != null)
        {
            return portOption.OptionValue as string;
        }
        return portOption?.DefaultValue as string;
    }
    
    public void SetWebApiHost(string? host)
    {
        Settings.WebApiHost = host;
        SaveSettings();
    }
    
    public void SetWebApiPort(string? port)
    {
        Settings.WebApiPort = port;
        SaveSettings();
    }

    public void SetFirstLaunchSetupComplete(bool value)
    {
        Settings.FirstLaunchSetupComplete = value;
        SaveSettings();
    }
    
    public void SetModelBrowserNsfwEnabled(bool value)
    {
        Settings.ModelBrowserNsfwEnabled = value;
        ModelBrowserNsfwEnabledChanged?.Invoke(this, value);
        SaveSettings();
    }

    public void SetSharedFolderCategoryVisible(SharedFolderType type, bool visible)
    {
        Settings.SharedFolderVisibleCategories ??= new SharedFolderType();
        if (visible)
        {
            Settings.SharedFolderVisibleCategories |= type;
        }
        else
        {
            Settings.SharedFolderVisibleCategories &= ~type;
        }
        SaveSettings();
    }
    
    public bool IsSharedFolderCategoryVisible(SharedFolderType type)
    {
        // False for default
        if (type == 0) return false;
        return Settings.SharedFolderVisibleCategories?.HasFlag(type) ?? false;
    }
    
    /// <summary>
    /// Loads settings from the settings file
    /// If the settings file does not exist, it will be created with default values
    /// </summary>
    private void LoadSettings()
    {
        FileLock.EnterReadLock();
        try
        {
            if (!File.Exists(SettingsPath))
            {
                File.Create(SettingsPath).Close();
                Settings.Theme = "Dark";
                Settings.WindowBackdropType = WindowBackdropType.Mica;
                var defaultSettingsJson = JsonSerializer.Serialize(Settings);
                File.WriteAllText(SettingsPath, defaultSettingsJson);
                return;
            }

            var settingsContent = File.ReadAllText(SettingsPath);
            Settings = JsonSerializer.Deserialize<Settings>(settingsContent, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            })!;
        }
        finally
        {
            FileLock.ExitReadLock();
        }
    }

    private void SaveSettings()
    {
        FileLock.TryEnterWriteLock(1000);
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
            File.WriteAllText(SettingsPath, json);
        }
        finally
        {
            FileLock.ExitWriteLock();
        }
    }
}
