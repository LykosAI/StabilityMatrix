using System.ComponentModel;
using System.Linq.Expressions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Services;

public interface ISettingsManager
{
    bool IsPortableMode { get; }
    DirectoryPath LibraryDir { get; }
    bool IsLibraryDirSet { get; }

    string ModelsDirectory { get; }
    string DownloadsDirectory { get; }
    DirectoryPath TagsDirectory { get; }
    DirectoryPath ImagesDirectory { get; }
    DirectoryPath ImagesInferenceDirectory { get; }
    DirectoryPath ConsolidatedImagesDirectory { get; }

    Settings Settings { get; }

    List<string> PackageInstallsInProgress { get; set; }
    DirectoryPath WorkflowDirectory { get; }
    DirectoryPath ExtensionPackDirectory { get; }

    /// <summary>
    /// Event fired when the library directory is changed
    /// </summary>
    event EventHandler<string>? LibraryDirChanged;

    /// <summary>
    /// Event fired when a property of Settings is changed
    /// </summary>
    event EventHandler<RelayPropertyChangedEventArgs>? SettingsPropertyChanged;

    /// <summary>
    /// Event fired when Settings are loaded from disk
    /// </summary>
    event EventHandler? Loaded;

    /// <summary>
    /// Set an override for the library directory.
    /// </summary>
    void SetLibraryDirOverride(DirectoryPath path);

    /// <summary>
    /// Register a handler that fires once when LibraryDir is first set.
    /// Will fire instantly if it is already set.
    /// </summary>
    void RegisterOnLibraryDirSet(Action<string> handler);

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
    /// <param name="ignoreMissingLibraryDir">Ignore missing library dir when committing changes</param>
    void Transaction(Action<Settings> func, bool ignoreMissingLibraryDir = false);

    /// <summary>
    /// Modify a settings property by expression and commit changes.
    /// This will notify listeners of SettingsPropertyChanged.
    /// </summary>
    void Transaction<TValue>(Expression<Func<Settings, TValue>> expression, TValue value);

    /// <summary>
    /// Register a source observable object and property to be relayed to Settings
    /// </summary>
    IDisposable RelayPropertyFor<T, TValue>(
        T source,
        Expression<Func<T, TValue>> sourceProperty,
        Expression<Func<Settings, TValue>> settingsProperty,
        bool setInitial = false,
        TimeSpan? delay = null
    )
        where T : INotifyPropertyChanged;

    /// <summary>
    /// Register an Action to be called on change of the settings property.
    /// </summary>
    IDisposable RegisterPropertyChangedHandler<T>(
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

    void SaveLaunchArgs(Guid packageId, IEnumerable<LaunchOption> launchArgs);
    bool IsEulaAccepted();
    void SetEulaAccepted();

    /// <summary>
    /// Cancels any scheduled delayed save of settings and flushes immediately.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken);
}
