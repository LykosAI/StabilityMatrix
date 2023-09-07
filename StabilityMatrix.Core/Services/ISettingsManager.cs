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
    Settings Settings { get; }
    List<string> PackageInstallsInProgress { get; set; }
    event EventHandler<string>? LibraryDirChanged;
    event EventHandler<PropertyChangedEventArgs>? SettingsPropertyChanged;

    /// <summary>
    /// Register a handler that fires once when LibraryDir is first set.
    /// Will fire instantly if it is already set.
    /// </summary>
    void RegisterOnLibraryDirSet(Action<string> handler);

    /// <inheritdoc />
    SettingsTransaction BeginTransaction();

    /// <inheritdoc />
    void Transaction(Action<Settings> func, bool ignoreMissingLibraryDir = false);

    /// <inheritdoc />
    void Transaction<TValue>(Expression<Func<Settings, TValue>> expression, TValue value);

    /// <inheritdoc />
    void RelayPropertyFor<T, TValue>(
        T source,
        Expression<Func<T, TValue>> sourceProperty,
        Expression<Func<Settings, TValue>> settingsProperty
    )
        where T : INotifyPropertyChanged;

    /// <inheritdoc />
    void RegisterPropertyChangedHandler<T>(
        Expression<Func<Settings, T>> settingsProperty,
        Action<T> onPropertyChanged
    );

    /// <summary>
    /// Attempts to locate and set the library path
    /// Return true if found, false otherwise
    /// </summary>
    bool TryFindLibrary();

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

    void UpdatePackageVersionNumber(Guid id, InstalledPackageVersion? newVersion);
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
