using System;
using System.Collections.Generic;
using System.Linq;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Models.Settings;

public class Settings
{
    public int? Version { get; set; } = 1;
    public bool FirstLaunchSetupComplete { get; set; }
    public string? Theme { get; set; } = "Dark";

    public WindowBackdropType? WindowBackdropType { get; set; } =
        Wpf.Ui.Controls.Window.WindowBackdropType.Mica;
    public List<InstalledPackage> InstalledPackages { get; set; } = new();
    public Guid? ActiveInstalledPackage { get; set; }
    public bool IsNavExpanded { get; set; }
    public bool HasSeenWelcomeNotification { get; set; }
    public List<string>? PathExtensions { get; set; }
    public string? WebApiHost { get; set; }
    public string? WebApiPort { get; set; }
    public bool ModelBrowserNsfwEnabled { get; set; }

    public SharedFolderType? SharedFolderVisibleCategories { get; set; } =
        SharedFolderType.StableDiffusion | 
        SharedFolderType.Lora | 
        SharedFolderType.LyCORIS;

    public string? Placement { get; set; }

    public ModelSearchOptions? ModelSearchOptions { get; set; }

    public InstalledPackage? GetActiveInstalledPackage()
    {
        return InstalledPackages.FirstOrDefault(x => x.Id == ActiveInstalledPackage);
    }
}
