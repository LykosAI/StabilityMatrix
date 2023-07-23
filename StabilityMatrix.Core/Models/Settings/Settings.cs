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

    public WindowSettings? WindowSettings { get; set; }

    public ModelSearchOptions? ModelSearchOptions { get; set; }
    
    public bool RemoveFolderLinksOnShutdown { get; set; }

    public HashSet<string>? InstalledModelHashes { get; set; } = new();

    public InstalledPackage? GetActiveInstalledPackage()
    {
        return GetInstalledPackageById(ActiveInstalledPackage);
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
    
    /// <summary>
    /// Get a package by Id
    /// Return null if not found
    /// </summary>
    public InstalledPackage? GetInstalledPackageById(Guid? id)
    {
        return id == null ? null : 
            InstalledPackages.FirstOrDefault(x => x.Id == id);
    }
    
    /// <summary>
    /// Update ActiveInstalledPackage if not valid
    /// uses first package or null if no packages
    /// </summary>
    public void UpdateActiveInstalledPackage()
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
