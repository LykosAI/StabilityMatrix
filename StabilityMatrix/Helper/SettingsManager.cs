using System;
using System.IO;
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
            File.WriteAllText(SettingsPath, "{}");
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
    
    public void SetActiveInstalledPackage(InstalledPackage? p) 
    {
        if (p == null)
        {
            Settings.ActiveInstalledPackage = null;
        }
        else
        {
            Settings.ActiveInstalledPackage = p.Id;
        }
        SaveSettings();
    }

    private void LoadSettings()
    {
        var settingsContent = File.ReadAllText(SettingsPath);
        Settings = JsonSerializer.Deserialize<Settings>(settingsContent)!;
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings);
        File.WriteAllText(SettingsPath, json);
    }
}
