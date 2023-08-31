using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallPackageStep : IPackageStep
{
    private readonly BasePackage package;
    private readonly TorchVersion torchVersion;
    private readonly string installPath;

    public InstallPackageStep(BasePackage package, TorchVersion torchVersion, string installPath)
    {
        this.package = package;
        this.torchVersion = torchVersion;
        this.installPath = installPath;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        package.ConsoleOutput += (sender, output) =>
        {
            progress?.Report(new ProgressReport
            {
                IsIndeterminate = true,
                Message = output.Text
            });
        };
        await package.InstallPackage(installPath, torchVersion, progress).ConfigureAwait(false);
    }

    public string ProgressTitle => "Installing package...";
}
