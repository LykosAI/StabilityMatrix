using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class AddInstalledPackageStep : IPackageStep
{
    private readonly ISettingsManager settingsManager;
    private readonly InstalledPackage newInstalledPackage;

    public AddInstalledPackageStep(
        ISettingsManager settingsManager,
        InstalledPackage newInstalledPackage
    )
    {
        this.settingsManager = settingsManager;
        this.newInstalledPackage = newInstalledPackage;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        if (!string.IsNullOrWhiteSpace(newInstalledPackage.DisplayName))
        {
            settingsManager.PackageInstallsInProgress.Remove(newInstalledPackage.DisplayName);
        }

        await using var transaction = settingsManager.BeginTransaction();
        transaction.Settings.InstalledPackages.Add(newInstalledPackage);
        transaction.Settings.ActiveInstalledPackageId = newInstalledPackage.Id;
    }

    public string ProgressTitle => $"{newInstalledPackage.DisplayName} Installed";
}
