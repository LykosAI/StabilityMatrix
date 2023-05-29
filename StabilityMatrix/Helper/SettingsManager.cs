﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StabilityMatrix.Models;

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

    public void UpdatePackageVersionNumber(string name, string? newVersion)
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Name == name);
        if (package == null || newVersion == null)
        {
            return;
        }
        
        package.PackageVersion = newVersion;
        SaveSettings();
    }
    
    public void SetLastUpdateCheck(InstalledPackage package)
    {
        Settings.InstalledPackages.First(p => p.Name == package.Name).LastUpdateCheck = package.LastUpdateCheck;
        SaveSettings();
    }
    
    public List<string> GetLaunchArgs(Guid packageId)
    {
        var packageData = Settings.InstalledPackages.FirstOrDefault(x => x.Id == packageId);
        return packageData?.LaunchArgs ?? new List<string>();
    }
    
    public void SaveLaunchArgs(Guid packageId, List<string> launchArgs)
    {
        var packageData = Settings.InstalledPackages.FirstOrDefault(x => x.Id == packageId);
        if (packageData == null)
        {
            return;
        }
        
        packageData.LaunchArgs = launchArgs;
        SaveSettings();
    }

    private void LoadSettings()
    {
        var settingsContent = File.ReadAllText(SettingsPath);
        Settings = JsonSerializer.Deserialize<Settings>(settingsContent)!;
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsPath, json);
    }
}