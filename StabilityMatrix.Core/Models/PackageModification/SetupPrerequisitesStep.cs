using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Models.PackageModification;

public class SetupPrerequisitesStep(
    IPrerequisiteHelper prerequisiteHelper,
    BasePackage package,
    PyVersion? pythonVersion = null
) : IPackageStep
{
    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        await prerequisiteHelper
            .InstallPackageRequirements(package, pythonVersion, progress)
            .ConfigureAwait(false);
    }

    public string ProgressTitle => "Installing prerequisites...";
}
