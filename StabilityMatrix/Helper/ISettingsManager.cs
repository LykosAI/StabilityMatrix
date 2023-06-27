using System;
using System.Collections.Generic;
using StabilityMatrix.Models;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Helper;

public interface ISettingsManager
{
    Settings Settings { get; }
    event EventHandler<bool>? ModelBrowserNsfwEnabledChanged;

    // Library settings
    bool IsPortableMode { get; }
    string LibraryDir { get; }
    bool TryFindLibrary();
    
    // Dynamic paths from library
    string DatabasePath { get; }
    string ModelsDirectory { get; }
    
    // Migration
    IEnumerable<InstalledPackage> GetOldInstalledPackages();

    void SetTheme(string theme);
    void AddInstalledPackage(InstalledPackage p);
    void RemoveInstalledPackage(InstalledPackage p);
    void SetActiveInstalledPackage(InstalledPackage? p);
    void SetNavExpanded(bool navExpanded);
    void AddPathExtension(string pathExtension);
    string GetPathExtensionsAsString();

    /// <summary>
    /// Insert path extensions to the front of the PATH environment variable
    /// </summary>
    void InsertPathExtensions();

    void UpdatePackageVersionNumber(Guid id, string? newVersion);
    void SetLastUpdateCheck(InstalledPackage package);
    List<LaunchOption> GetLaunchArgs(Guid packageId);
    void SaveLaunchArgs(Guid packageId, List<LaunchOption> launchArgs);
    void SetWindowBackdropType(WindowBackdropType backdropType);
    void SetHasSeenWelcomeNotification(bool hasSeenWelcomeNotification);
    string? GetActivePackageHost();
    string? GetActivePackagePort();
    void SetWebApiHost(string? host);
    void SetWebApiPort(string? port);
    void SetFirstLaunchSetupComplete(bool firstLaunchSetupCompleted);
    void SetModelBrowserNsfwEnabled(bool value);
    void SetSharedFolderCategoryVisible(SharedFolderType type, bool visible);
    bool IsSharedFolderCategoryVisible(SharedFolderType type);
    bool IsEulaAccepted();
    void SetEulaAccepted();

    /// <summary>
    /// Save a new library path to %APPDATA%/StabilityMatrix/library.json
    /// </summary>
    void SetLibraryPath(string path);

    /// <summary>
    /// Enable and create settings files for portable mode
    /// Creates the ./Data directory and the `.sm-portable` marker file
    /// </summary>
    void SetPortableMode();
}
