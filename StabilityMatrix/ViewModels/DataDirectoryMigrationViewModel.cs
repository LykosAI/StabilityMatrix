using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix.ViewModels;

public partial class DataDirectoryMigrationViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;

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
    
    public string MigrateProgressText => $"Migrating {MigrateProgressCount} of {AutoMigrateCount + NeedsMoveMigrateCount} Packages";

    public DataDirectoryMigrationViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
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
        var oldPackages = settingsManager.GetOldInstalledPackages();
        foreach (var package in oldPackages)
        {
            MigrateProgressCount++;
            await package.MigratePath();
        }
        // Need to save settings to commit our changes to installed packages
        settingsManager.SaveSettings();
    }
}
