using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class SetupModelFoldersStep : IPackageStep
{
    private readonly BasePackage package;
    private readonly SharedFolderMethod sharedFolderMethod;
    private readonly string installPath;

    public SetupModelFoldersStep(
        BasePackage package,
        SharedFolderMethod sharedFolderMethod,
        string installPath
    )
    {
        this.package = package;
        this.sharedFolderMethod = sharedFolderMethod;
        this.installPath = installPath;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(
            new ProgressReport(-1f, "Setting up shared folder links...", isIndeterminate: true)
        );
        await package.SetupModelFolders(installPath, sharedFolderMethod).ConfigureAwait(false);
    }

    public string ProgressTitle => "Setting up shared folder links...";
}
