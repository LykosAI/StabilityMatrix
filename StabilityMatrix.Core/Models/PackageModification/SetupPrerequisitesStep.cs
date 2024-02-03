using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Models.PackageModification;

public class SetupPrerequisitesStep : IPackageStep
{
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IPyRunner pyRunner;
    private readonly BasePackage package;

    public SetupPrerequisitesStep(
        IPrerequisiteHelper prerequisiteHelper,
        IPyRunner pyRunner,
        BasePackage package
    )
    {
        this.prerequisiteHelper = prerequisiteHelper;
        this.pyRunner = pyRunner;
        this.package = package;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        // package and platform-specific requirements install
        await prerequisiteHelper.InstallPackageRequirements(package, progress).ConfigureAwait(false);
    }

    public string ProgressTitle => "Installing prerequisites...";
}
