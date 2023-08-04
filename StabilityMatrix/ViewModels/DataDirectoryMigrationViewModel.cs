using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.ViewModels;

public partial class DataDirectoryMigrationViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISettingsManager settingsManager;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IPyRunner pyRunner;
    private readonly ISharedFolders sharedFolders;
    private readonly IPackageFactory packageFactory;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(AutoMigrateText))]
    [NotifyPropertyChangedFor(nameof(MigrateProgressText))]
    private int autoMigrateCount;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(NeedsMoveMigrateText))]
    [NotifyPropertyChangedFor(nameof(MigrateProgressText))]
    private int needsMoveMigrateCount;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(MigrateProgressText))]
    private int migrateProgressCount;
    
    [ObservableProperty] private bool isMigrating;
    [ObservableProperty] private bool canShowNoThanksButton;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(NeedsMoveMigrateText))]
    [NotifyPropertyChangedFor(nameof(MigrateProgressText))]
    private bool hasFreeSpaceError;

    public string AutoMigrateText => AutoMigrateCount == 0 ? string.Empty :
        $"{AutoMigrateCount} Packages will be automatically migrated to the new format";

    public string NeedsMoveMigrateText => NeedsMoveMigrateCount == 0 || HasFreeSpaceError
        ? string.Empty
        : $"{NeedsMoveMigrateCount} Package{(NeedsMoveMigrateCount > 1 ? "s" : "")} {(NeedsMoveMigrateCount > 1 ? "are" : "is")} " +
          "not relative to the Data Directory and will be moved, this may take a few minutes";

    [ObservableProperty]
    private string migrateProgressText = "";

    partial void OnMigrateProgressCountChanged(int value)
    {
        MigrateProgressText = value > 0 ? $"Migrating {value} of {AutoMigrateCount + NeedsMoveMigrateCount} Packages" : string.Empty;
    }

    public DataDirectoryMigrationViewModel(ISettingsManager settingsManager,
        IPrerequisiteHelper prerequisiteHelper, IPyRunner pyRunner, ISharedFolders sharedFolders,
        IPackageFactory packageFactory)
    {
        this.settingsManager = settingsManager;
        this.prerequisiteHelper = prerequisiteHelper;
        this.pyRunner = pyRunner;
        this.sharedFolders = sharedFolders;
        this.packageFactory = packageFactory;
    }

    public async Task OnLoaded()
    {
        AutoMigrateCount = 0;
        NeedsMoveMigrateCount = 0;
        HasFreeSpaceError = false;
        
        // Get all old packages
        var oldPackages = settingsManager.GetOldInstalledPackages().ToArray();
        // Attempt to migrate with pure, and count successful migrations
        AutoMigrateCount = oldPackages.Count(p => p.CanPureMigratePath());
        // Any remaining packages need to be moved
        NeedsMoveMigrateCount = oldPackages.Length - AutoMigrateCount;

        var oldLibraryDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StabilityMatrix");
        CanShowNoThanksButton = settingsManager.LibraryDir != oldLibraryDir;
        

        if (settingsManager.LibraryDir != oldLibraryDir)
        {
            var oldDir = new DirectoryPath(oldLibraryDir);
            var size = await oldDir.GetSizeAsync(includeSymbolicLinks: false);

            // If there's not enough space in the new DataDirectory, show warning
            if (size > new DriveInfo(settingsManager.LibraryDir).AvailableFreeSpace)
            {
                HasFreeSpaceError = true;
            }
        }
    }
    
    public void CleanupOldInstall()
    {
        var oldLibraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix");
        if (settingsManager.LibraryDir == oldLibraryPath) return;
        
        // rename old settings.json to settings.json.old
        var oldSettingsPath = Path.Combine(oldLibraryPath, "settings.json");
        File.Move(oldSettingsPath, oldSettingsPath + ".old", true);
            
        // delete old PortableGit dir
        var oldPortableGitDir = Path.Combine(oldLibraryPath, "PortableGit");
        if (Directory.Exists(oldPortableGitDir))
        {
            Directory.Delete(oldPortableGitDir, true);
        }
            
        // delete old Assets dir
        var oldAssetsDir = Path.Combine(oldLibraryPath, "Assets");
        if (Directory.Exists(oldAssetsDir))
        {
            Directory.Delete(oldAssetsDir, true);
        }
    }
    
    [RelayCommand]
    private async Task MigrateAsync()
    {
        await using var delay = new MinimumDelay(200, 300);

        IsMigrating = true;
        
        // Since we are going to recreate venvs, need python to be installed
        if (!prerequisiteHelper.IsPythonInstalled)
        {
            MigrateProgressText = "Preparing Environment...";
            await prerequisiteHelper.InstallPythonIfNecessary();
        }
        
        if (!PyRunner.PipInstalled)
        {
            await pyRunner.SetupPip();
        }
            
        if (!PyRunner.VenvInstalled)
        {
            await pyRunner.InstallPackage("virtualenv");
        }

        var libraryPath = settingsManager.LibraryDir;
        var oldLibraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix");
        var oldPackages = settingsManager.GetOldInstalledPackages().ToArray();

        MigrateProgressText = "Migrating Packages...";
        
        await using var st = settingsManager.BeginTransaction();
        
        foreach (var package in oldPackages)
        {
            st.Settings.RemoveInstalledPackageAndUpdateActive(package);

#pragma warning disable CS0618
            Logger.Info($"Migrating package {MigrateProgressCount} of {oldPackages.Length} at path {package.Path}");
#pragma warning restore CS0618
            
            await package.MigratePath();
            MigrateProgressCount++;
            st.Settings.InstalledPackages.Add(package);
            st.Settings.ActiveInstalledPackageId = package.Id;

            if (oldLibraryPath != libraryPath)
            {
                // setup model links again
                if (!string.IsNullOrWhiteSpace(package.PackageName))
                {
                    var basePackage = packageFactory.FindPackageByName(package.PackageName);
                    if (basePackage != default)
                    {
                        sharedFolders.SetupLinksForPackage(basePackage, package.FullPath!);
                    }
                }
            }

            // Also recreate the venv
            var venvPath = Path.Combine(libraryPath, package.FullPath!);
            var venv = new PyVenvRunner(venvPath);
            await venv.Setup(existsOk: true);
        }

        // Copy models directory
        if (oldLibraryPath != libraryPath)
        {
            MigrateProgressText = "Copying models...";
            var oldModelsDir = Path.Combine(oldLibraryPath, "Models");
            var newModelsDir = Path.Combine(libraryPath, "Models");
            await Task.Run(() => Utilities.CopyDirectory(oldModelsDir, newModelsDir, true));

            MigrateProgressText = "Cleaning up...";
            await Task.Run(CleanupOldInstall);
        }

        IsMigrating = false;
        EventManager.Instance.OnInstalledPackagesChanged();
    }
}
