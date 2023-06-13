using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Api;
using StabilityMatrix.Models;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Helper;

public class SettingsManager : ISettingsManager
{
    private const string SettingsFileName = "settings.json";
    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StabilityMatrix",
            SettingsFileName);
    private readonly string? originalEnvPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);

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
            Settings.Theme = "Dark";
            Settings.WindowBackdropType = WindowBackdropType.Mica;
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
    
    public void AddPathExtension(string pathExtension)
    {
        Settings.PathExtensions ??= new List<string>();
        Settings.PathExtensions.Add(pathExtension);
        SaveSettings();
    }

    public string GetPathExtensionsAsString()
    {
        return string.Join(";", Settings.PathExtensions ?? new List<string>());
    }

    /// <summary>
    /// Insert path extensions to the front of the PATH environment variable
    /// </summary>
    public void InsertPathExtensions()
    {
        if (Settings.PathExtensions == null) return;
        var toInsert = GetPathExtensionsAsString();
        // Append the original path, if any
        if (originalEnvPath != null)
        {
            toInsert += $";{originalEnvPath}";
        }
        Environment.SetEnvironmentVariable("PATH", toInsert, EnvironmentVariableTarget.Process);
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
    
    public void SetHasSeenWelcomeNotification(bool hasSeenWelcomeNotification)
    {
        Settings.HasSeenWelcomeNotification = hasSeenWelcomeNotification;
        SaveSettings();
    }
    
    public string? GetActivePackageHost()
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Id == Settings.ActiveInstalledPackage);
        if (package == null) return null;
        var hostOption = package.LaunchArgs?.FirstOrDefault(x => x.Name.ToLowerInvariant() == "host");
        if (hostOption?.OptionValue != null)
        {
            return hostOption.OptionValue as string;
        }
        return hostOption?.DefaultValue as string;
    }

    public string? GetActivePackagePort()
    {
        var package = Settings.InstalledPackages.FirstOrDefault(x => x.Id == Settings.ActiveInstalledPackage);
        if (package == null) return null;
        var portOption = package.LaunchArgs?.FirstOrDefault(x => x.Name.ToLowerInvariant() == "port");
        if (portOption?.OptionValue != null)
        {
            return portOption.OptionValue as string;
        }
        return portOption?.DefaultValue as string;
    }
    
    public void SetWebApiHost(string? host)
    {
        Settings.WebApiHost = host;
        SaveSettings();
    }
    
    public void SetWebApiPort(string? port)
    {
        Settings.WebApiPort = port;
        SaveSettings();
    }
    
    public void SetModelsDirectory(string? directory)
    {
        Settings.ModelsDirectory = directory;
        SaveSettings();
    }
    
    public void SetFirstLaunchSetupComplete(bool value)
    {
        Settings.FirstLaunchSetupComplete = value;
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
