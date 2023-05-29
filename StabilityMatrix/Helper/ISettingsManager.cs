using System;
using System.Collections.Generic;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface ISettingsManager
{
    Settings Settings { get; }
    void SetTheme(string theme);
    void AddInstalledPackage(InstalledPackage p);
    void SetActiveInstalledPackage(InstalledPackage? p);
    void SetHasInstalledPip(bool hasInstalledPip);
    void SetHasInstalledVenv(bool hasInstalledVenv);
    void SetNavExpanded(bool navExpanded);
    void UpdatePackageVersionNumber(string packageName, string newVersion);
    List<string> GetLaunchArgs(Guid packageId);
    void SaveLaunchArgs(Guid packageId, List<string> launchArgs);
}
