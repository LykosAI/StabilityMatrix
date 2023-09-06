using System.ComponentModel;
using System.Linq.Expressions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

public interface ISettingsManager
{
    bool IsPortableMode { get; }
    string LibraryDir { get; }
    bool IsLibraryDirSet { get; }
    string DatabasePath { get; }
    string ModelsDirectory { get; }
    string DownloadsDirectory { get; }
    DirectoryPath TagsDirectory { get; }
    DirectoryPath ImagesDirectory { get; }

    Settings Settings { get; }

    /// <summary>
    /// Event fired when the library directory is changed
    /// </summary>
    event EventHandler<string>? LibraryDirChanged;

    /// <summary>
    /// Event fired when a property of Settings is changed
    /// </summary>
    event EventHandler<RelayPropertyChangedEventArgs>? SettingsPropertyChanged;

    /// <summary>
    /// Register a handler that fires once when LibraryDir is first set.
    /// Will fire instantly if it is already set.
    /// </summary>
    void RegisterOnLibraryDirSet(Action<string> handler);

    /// <summary>
    /// Event fired when Settings are loaded from disk
    /// </summary>
    event EventHandler? Loaded;

    /// <summary>
    /// Return a SettingsTransaction that can be used to modify Settings
    /// Saves on Dispose.
    /// </summary>
    SettingsTransaction BeginTransaction();

    /// <summary>
    /// Execute a function that modifies Settings
    /// Commits changes after the function returns.
    /// </summary>
    /// <param name="func">Function accepting Settings to modify</param>
    void Transaction(Action<Settings> func, bool ignoreMissingLibraryDir = false);

    /// <summary>
    /// Modify a settings property by expression and commit changes.
    /// This will notify listeners of SettingsPropertyChanged.
    /// </summary>
    void Transaction<TValue>(Expression<Func<Settings, TValue>> expression, TValue value);

    /// <summary>
    /// Register a source observable object and property to be relayed to Settings
    /// </summary>
    void RelayPropertyFor<T, TValue>(
        T source,
        Expression<Func<T, TValue>> sourceProperty,
        Expression<Func<Settings, TValue>> settingsProperty,
        bool setInitial = false
    )
        where T : INotifyPropertyChanged;

    /// <summary>
    /// Register an Action to be called on change of the settings property.
    /// </summary>
    void RegisterPropertyChangedHandler<T>(
        Expression<Func<Settings, T>> settingsProperty,
        Action<T> onPropertyChanged
    );

    /// <summary>
    /// Attempts to locate and set the library path
    /// Return true if found, false otherwise
    /// </summary>
    /// <param name="forceReload">Force reload even if library is already set</param>
    bool TryFindLibrary(bool forceReload = false);

    /// <summary>
    /// Save a new library path to %APPDATA%/StabilityMatrix/library.json
    /// </summary>
    void SetLibraryPath(string path);

    /// <summary>
    /// Enable and create settings files for portable mode
    /// Creates the ./Data directory and the `.sm-portable` marker file
    /// </summary>
    void SetPortableMode();

    /// <summary>
    /// Iterable of installed packages using the old absolute path format.
    /// Can be called with Any() to check if the user needs to migrate.
    /// </summary>
    IEnumerable<InstalledPackage> GetOldInstalledPackages();

    Guid GetOldActivePackageId();
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
    string? GetActivePackageHost();
    string? GetActivePackagePort();
    void SetSharedFolderCategoryVisible(SharedFolderType type, bool visible);
    bool IsSharedFolderCategoryVisible(SharedFolderType type);
    bool IsEulaAccepted();
    void SetEulaAccepted();
    void IndexCheckpoints();
}
