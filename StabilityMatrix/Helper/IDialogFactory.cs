using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IDialogFactory
{
    LaunchOptionsDialog CreateLaunchOptionsDialog(IEnumerable<LaunchOptionDefinition> definitions, InstalledPackage installedPackage);
    InstallerWindow CreateInstallerWindow();
    OneClickInstallDialog CreateOneClickInstallDialog();
    Task<List<string>?> ShowTextEntryDialog(string title, IEnumerable<(string, string)> fieldNames,
        string closeButtonText = "Cancel", string saveButtonText = "Save");
}
