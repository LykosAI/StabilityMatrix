using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Settings;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Helper;

public interface ISettingsManager
{
    Settings Settings { get; }
    
    // Events
    event EventHandler<string>? LibraryDirChanged; 
    
    // Library settings
    bool IsPortableMode { get; }
    string LibraryDir { get; }
    bool IsLibraryDirSet { get; }
    bool TryFindLibrary();
    
    // Dynamic paths from library
    string DatabasePath { get; }
    string ModelsDirectory { get; }

    /// <summary>
    /// Return a SettingsTransaction that can be used to modify Settings
    /// Saves on Dispose.
    /// </summary>
    public SettingsTransaction BeginTransaction();

    /// <summary>
    /// Execute a function that modifies Settings
    /// Commits changes after the function returns.
    /// </summary>
    /// <param name="func">Function accepting Settings to modify</param>
    public void Transaction(Action<Settings> func);

    /// <summary>
    /// Modify a settings property by expression and commit changes.
    /// This will notify listeners of SettingsPropertyChanged.
    /// </summary>
    public void Transaction<TValue>(Expression<Func<Settings, TValue>> expression, TValue value);

    /// <summary>
    /// Register a source observable object and property to be relayed to Settings
    /// </summary>
    public void RelayPropertyFor<T, TValue>(
        T source,
        Expression<Func<T, TValue>> sourceProperty,
        Expression<Func<Settings, TValue>> settingsProperty) where T : INotifyPropertyChanged;

    /// <summary>
    /// Register an Action to be called on change of the settings property.
    /// </summary>
    public void RegisterPropertyChangedHandler<T>(
        Expression<Func<Settings, T>> settingsProperty,
        Action<T> onPropertyChanged);
    
    // Migration
    IEnumerable<InstalledPackage> GetOldInstalledPackages();
    
    void AddPathExtension(string pathExtension);

    /// <summary>
    /// Insert path extensions to the front of the PATH environment variable
    /// </summary>
    void InsertPathExtensions();

    void UpdatePackageVersionNumber(Guid id, string? newVersion);
    void SetLastUpdateCheck(InstalledPackage package);
    List<LaunchOption> GetLaunchArgs(Guid packageId);
    void SaveLaunchArgs(Guid packageId, List<LaunchOption> launchArgs);
    string? GetActivePackageHost();
    string? GetActivePackagePort();
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
    Guid GetOldActivePackageId();
}
