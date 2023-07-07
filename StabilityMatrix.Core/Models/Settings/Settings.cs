namespace StabilityMatrix.Core.Models.Settings;

public class Settings
{
    public int? Version { get; set; } = 1;
    public bool FirstLaunchSetupComplete { get; set; }
    public string? Theme { get; set; } = "Dark";

    public List<InstalledPackage> InstalledPackages { get; set; } = new();
    public Guid? ActiveInstalledPackage { get; set; }
    public bool HasSeenWelcomeNotification { get; set; }
    public List<string>? PathExtensions { get; set; }
    public string? WebApiHost { get; set; }
    public string? WebApiPort { get; set; }
    
    // UI states
    public bool ModelBrowserNsfwEnabled { get; set; }
    public bool IsNavExpanded { get; set; }
    public bool IsImportAsConnected { get; set; }
    public SharedFolderType? SharedFolderVisibleCategories { get; set; } =
        SharedFolderType.StableDiffusion | 
        SharedFolderType.Lora | 
        SharedFolderType.LyCORIS;

    public string? Placement { get; set; }

    public ModelSearchOptions? ModelSearchOptions { get; set; }
    
    public bool KeepFolderLinksOnShutdown { get; set; }

    public InstalledPackage? GetActiveInstalledPackage()
    {
        return InstalledPackages.FirstOrDefault(x => x.Id == ActiveInstalledPackage);
    }

    public void RemoveInstalledPackageAndUpdateActive(InstalledPackage package)
    {
        RemoveInstalledPackageAndUpdateActive(package.Id);
    }
    
    public void RemoveInstalledPackageAndUpdateActive(Guid id)
    {
        InstalledPackages.RemoveAll(x => x.Id == id);
        UpdateActiveInstalledPackage();
    }
    
    // Update ActiveInstalledPackage if not valid
    // uses first package or null if no packages
    private void UpdateActiveInstalledPackage()
    {
        // Empty packages - set to null
        if (InstalledPackages.Count == 0)
        {
            ActiveInstalledPackage = null;
        }
        // Active package is not in package - set to first package
        else if (InstalledPackages.All(x => x.Id != ActiveInstalledPackage))
        {
            ActiveInstalledPackage = InstalledPackages[0].Id;
        }
    }
}
