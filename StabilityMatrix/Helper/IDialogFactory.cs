using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Helper;

public interface IDialogFactory
{
    LaunchOptionsDialog CreateLaunchOptionsDialog(IEnumerable<LaunchOptionDefinition> definitions, InstalledPackage installedPackage);

    /// <summary>
    /// Creates a dialog that allows the user to enter text for each field name.
    /// Return a list of strings that correspond to the field names.
    /// If cancel is pressed, return null.
    /// <param name="fields">List of (fieldName, placeholder)</param>
    /// </summary>
    Task<List<string>?> ShowTextEntryDialog(string title, 
        IEnumerable<(string, string)> fields, 
        string closeButtonText = "Cancel",
        string saveButtonText = "Save");

    /// <summary>
    /// Creates and shows a confirmation dialog.
    /// Return true if the user clicks the primary button.
    /// </summary>
    Task<bool> ShowConfirmationDialog(string title, string message, string closeButtonText = "Cancel", string primaryButtonText = "Confirm");

    OneClickInstallDialog CreateOneClickInstallDialog();
    InstallerWindow CreateInstallerWindow();
    SelectInstallLocationsDialog CreateInstallLocationsDialog();
    DataDirectoryMigrationDialog CreateDataDirectoryMigrationDialog();
    WebLoginDialog CreateWebLoginDialog();
    SelectModelVersionDialog CreateSelectModelVersionDialog(CivitModel model);
}
