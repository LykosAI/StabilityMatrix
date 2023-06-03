using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Models;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Helper;

public class SettingsManager : ISettingsManager
{
    private const string SettingsFileName = "settings.json";
    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            SettingsFileName);

    public Settings Settings { get; private set; } = new();

    public SettingsManager()
    {
        if (!Directory.Exists(SettingsPath.Replace(SettingsFileName, "")))
        {
            Directory.CreateDirectory(SettingsPath.Replace(SettingsFileName, ""));
        }

        if (!File.Exists(SettingsPath))
        {
            File.Create(SettingsPath).Close();
            var defaultSettingsJson = JsonSerializer.Serialize(Settings);
            File.WriteAllText(SettingsPath, defaultSettingsJson);
        }

        LoadSettings();
    }

    public void SetTheme(string theme)
    {
        Settings.Theme = theme;
        SaveSettings();
    }

    public void AddInstalledPackage(InstalledPackage p)
    {
        Settings.InstalledPackages.Add(p);
        SaveSettings();
    }

    public void RemoveInstalledPackage(InstalledPackage p)
    {
        Settings.InstalledPackages.Remove(p);
        SetActiveInstalledPackage(Settings.InstalledPackages.Any() ? Settings.InstalledPackages.First() : null);
    }

    public void SetActiveInstalledPackage(InstalledPackage? p)
    {
        Settings.ActiveInstalledPackage = p?.Id;
        SaveSettings();
    }
    
    public void SetNavExpanded(bool navExpanded)
    {
        Settings.IsNavExpanded = navExpanded;
        SaveSettings();
    }

    public void UpdatePackageVersionNumber(Guid id, string? newVersion)
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Id == id);
        if (package == null || newVersion == null)
        {
            return;
        }

        package.PackageVersion = newVersion;

        package.DisplayVersion = string.IsNullOrWhiteSpace(package.InstalledBranch)
            ? newVersion
            : $"{package.InstalledBranch}@{newVersion[..7]}";

        SaveSettings();
    }
    
    public void SetLastUpdateCheck(InstalledPackage package)
    {
        Settings.InstalledPackages.First(p => p.DisplayName == package.DisplayName).LastUpdateCheck = package.LastUpdateCheck;
        SaveSettings();
    }
    
    public List<LaunchOption> GetLaunchArgs(Guid packageId)
    {
        var packageData = Settings.InstalledPackages.FirstOrDefault(x => x.Id == packageId);
        return packageData?.LaunchArgs ?? new();
    }
    
    public void SaveLaunchArgs(Guid packageId, List<LaunchOption> launchArgs)
    {
        var packageData = Settings.InstalledPackages.FirstOrDefault(x => x.Id == packageId);
        if (packageData == null)
        {
            return;
        }
        // Only save if not null or default
        var toSave = launchArgs.Where(opt => !opt.IsEmptyOrDefault()).ToList();

        packageData.LaunchArgs = toSave;
        SaveSettings();
    }

    public void SetWindowBackdropType(WindowBackdropType backdropType)
    {
        Settings.WindowBackdropType = backdropType;
        SaveSettings();
    }

    private void LoadSettings()
    {
        var settingsContent = File.ReadAllText(SettingsPath);
        Settings = JsonSerializer.Deserialize<Settings>(settingsContent, new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        })!;
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
        File.WriteAllText(SettingsPath, json);
    }
}
