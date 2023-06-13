using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Models;

public class Settings
{
    public bool FirstLaunchSetupComplete { get; set; }
    public string? Theme { get; set; }
    public WindowBackdropType? WindowBackdropType { get; set; }
    public List<InstalledPackage> InstalledPackages { get; set; } = new();
    public Guid? ActiveInstalledPackage { get; set; }
    public bool IsNavExpanded { get; set; }
    public bool HasSeenWelcomeNotification { get; set; }
    public List<string>? PathExtensions { get; set; }
    public string? WebApiHost { get; set; }
    public string? WebApiPort { get; set; }

    [NotNull]
    public string? ModelsDirectory { get; set; }
    
    /// <summary>
    /// Sets default values for settings
    /// </summary>
    [MemberNotNull(nameof(ModelsDirectory))]
    public void SetDefaults()
    {
        ModelsDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix", "Models");
    }

    public InstalledPackage? GetActiveInstalledPackage()
    {
        return InstalledPackages.FirstOrDefault(x => x.Id == ActiveInstalledPackage);
    }
}
