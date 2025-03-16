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
        // If user has selected a specific Python version, make sure it's installed
        if (pythonVersion.HasValue)
        {
            if (
                package.Prerequisites.Contains(PackagePrerequisite.Python310)
                || package.Prerequisites.Contains(PackagePrerequisite.Python31016)
            )
            {
                await prerequisiteHelper
                    .InstallPythonIfNecessary(pythonVersion.Value, progress)
                    .ConfigureAwait(false);
                await prerequisiteHelper
                    .InstallTkinterIfNecessary(pythonVersion.Value, progress)
                    .ConfigureAwait(false);
                await prerequisiteHelper
                    .InstallVirtualenvIfNecessary(pythonVersion.Value, progress)
                    .ConfigureAwait(false);
            }
        }

        // package and platform-specific requirements install (default behavior)
        await prerequisiteHelper.InstallPackageRequirements(package, progress).ConfigureAwait(false);
    }

    public string ProgressTitle => "Installing prerequisites...";
}
