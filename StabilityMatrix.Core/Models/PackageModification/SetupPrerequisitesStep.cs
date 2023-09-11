using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Models.PackageModification;

public class SetupPrerequisitesStep : IPackageStep
{
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IPyRunner pyRunner;

    public SetupPrerequisitesStep(IPrerequisiteHelper prerequisiteHelper, IPyRunner pyRunner)
    {
        this.prerequisiteHelper = prerequisiteHelper;
        this.pyRunner = pyRunner;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        // git, vcredist, etc...
        await prerequisiteHelper.InstallAllIfNecessary(progress).ConfigureAwait(false);

        // python stuff
        if (!PyRunner.PipInstalled || !PyRunner.VenvInstalled)
        {
            progress?.Report(
                new ProgressReport(-1f, "Installing Python prerequisites...", isIndeterminate: true)
            );

            await pyRunner.Initialize().ConfigureAwait(false);

            if (!PyRunner.PipInstalled)
            {
                await pyRunner.SetupPip().ConfigureAwait(false);
            }
            if (!PyRunner.VenvInstalled)
            {
                await pyRunner.InstallPackage("virtualenv").ConfigureAwait(false);
            }
        }
    }

    public string ProgressTitle => "Installing prerequisites...";
}
