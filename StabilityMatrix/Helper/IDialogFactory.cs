using System.Collections.Generic;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IDialogFactory
{
    LaunchOptionsDialog CreateLaunchOptionsDialog(IEnumerable<LaunchOptionDefinition> definitions, InstalledPackage installedPackage);
    InstallerWindow CreateInstallerWindow();
    OneClickInstallDialog CreateOneClickInstallDialog();
}
