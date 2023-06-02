using System;
using System.Collections.Generic;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface ISettingsManager
{
    Settings Settings { get; }
    void SetTheme(string theme);
    void AddInstalledPackage(InstalledPackage p);
    void RemoveInstalledPackage(InstalledPackage p);
    void SetActiveInstalledPackage(InstalledPackage? p);
    void SetNavExpanded(bool navExpanded);
    void UpdatePackageVersionNumber(Guid id, string? newVersion);
    void SetLastUpdateCheck(InstalledPackage package);
    List<LaunchOption> GetLaunchArgs(Guid packageId);
    void SaveLaunchArgs(Guid packageId, List<LaunchOption> launchArgs);
}
