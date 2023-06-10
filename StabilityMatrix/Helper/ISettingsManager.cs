using System;
using System.Collections.Generic;
using StabilityMatrix.Models;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.Helper;

public interface ISettingsManager
{
    Settings Settings { get; }
    void SetTheme(string theme);
    void AddInstalledPackage(InstalledPackage p);
    void RemoveInstalledPackage(InstalledPackage p);
    void SetActiveInstalledPackage(InstalledPackage? p);
    void SetNavExpanded(bool navExpanded);
    void AddPathExtension(string pathExtension);
    string GetPathExtensionsAsString();

    /// <summary>
    /// Insert path extensions to the front of the PATH environment variable
    /// </summary>
    void InsertPathExtensions();

    void UpdatePackageVersionNumber(Guid id, string? newVersion);
    void SetLastUpdateCheck(InstalledPackage package);
    List<LaunchOption> GetLaunchArgs(Guid packageId);
    void SaveLaunchArgs(Guid packageId, List<LaunchOption> launchArgs);
    void SetWindowBackdropType(WindowBackdropType backdropType);
    void SetHasSeenWelcomeNotification(bool hasSeenWelcomeNotification);
    string? GetActivePackageHost();
    string? GetActivePackagePort();
    void SetWebApiHost(string? host);
    void SetWebApiPort(string? port);
}
