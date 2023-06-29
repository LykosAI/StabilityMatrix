using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Helper;
using StabilityMatrix.Python;

namespace StabilityMatrix.ViewModels;

public partial class DataDirectoryMigrationViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISettingsManager settingsManager;
    private readonly IPrerequisiteHelper prerequisiteHelper;

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

    public string AutoMigrateText => AutoMigrateCount == 0 ? string.Empty :
        $"{AutoMigrateCount} Packages will be automatically migrated to the new format";
    
    public string NeedsMoveMigrateText => NeedsMoveMigrateCount == 0 ? string.Empty :
        $"{NeedsMoveMigrateCount} Packages are not relative to the Data Directory and will be moved, this may take a few minutes";

    [ObservableProperty]
    private string migrateProgressText = "";

    partial void OnMigrateProgressCountChanged(int value)
    {
        MigrateProgressText = value > 0 ? $"Migrating {value} of {AutoMigrateCount + NeedsMoveMigrateCount} Packages" : string.Empty;
    }
    
    public DataDirectoryMigrationViewModel(ISettingsManager settingsManager, IPrerequisiteHelper prerequisiteHelper)
    {
        this.settingsManager = settingsManager;
        this.prerequisiteHelper = prerequisiteHelper;
    }

    public void OnLoaded()
    {
        AutoMigrateCount = 0;
        NeedsMoveMigrateCount = 0;
        
        // Get all old packages
        var oldPackages = settingsManager.GetOldInstalledPackages().ToArray();
        // Attempt to migrate with pure, and count successful migrations
        AutoMigrateCount = oldPackages.Count(p => p.CanPureMigratePath());
        // Any remaining packages need to be moved
        NeedsMoveMigrateCount = oldPackages.Length - AutoMigrateCount;
    }
    
    [RelayCommand]
    private async Task MigrateAsync()
    {
        await using var delay = new MinimumDelay(200, 300);

        // Since we are going to recreate venvs, need python to be installed
        if (!prerequisiteHelper.IsPythonInstalled)
        {
            MigrateProgressText = "Preparing Environment";
            await prerequisiteHelper.InstallPythonIfNecessary();
        }

        var libraryPath = settingsManager.LibraryDir;
        var oldPackages = settingsManager.GetOldInstalledPackages().ToArray();

        foreach (var package in oldPackages)
        {
            MigrateProgressCount++;
#pragma warning disable CS0618
            Logger.Info($"Migrating package {MigrateProgressCount} of {oldPackages.Length} at path {package.Path}");
#pragma warning restore CS0618
            await package.MigratePath();
            
            // Save after each step in case interrupted
            settingsManager.SaveSettings();
            
            // Also recreate the venv
            var venvPath = Path.Combine(libraryPath, package.FullPath!);
            var venv = new PyVenvRunner(venvPath);
            await venv.Setup(existsOk: true);
        }

    }
}
