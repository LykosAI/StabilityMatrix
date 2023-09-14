using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class UpdatePackageStep : IPackageStep
{
    private readonly ISettingsManager settingsManager;
    private readonly InstalledPackage installedPackage;
    private readonly BasePackage basePackage;

    public UpdatePackageStep(
        ISettingsManager settingsManager,
        InstalledPackage installedPackage,
        BasePackage basePackage
    )
    {
        this.settingsManager = settingsManager;
        this.installedPackage = installedPackage;
        this.basePackage = basePackage;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        var torchVersion =
            installedPackage.PreferredTorchVersion ?? basePackage.GetRecommendedTorchVersion();

        void OnConsoleOutput(ProcessOutput output)
        {
            progress?.Report(new ProgressReport { IsIndeterminate = true, Message = output.Text });
        }

        var updateResult = await basePackage
            .Update(installedPackage, torchVersion, progress, onConsoleOutput: OnConsoleOutput)
            .ConfigureAwait(false);

        settingsManager.UpdatePackageVersionNumber(installedPackage.Id, updateResult);
        await using (settingsManager.BeginTransaction())
        {
            installedPackage.UpdateAvailable = false;
        }
    }

    public string ProgressTitle => $"Updating {installedPackage.DisplayName}";
}
