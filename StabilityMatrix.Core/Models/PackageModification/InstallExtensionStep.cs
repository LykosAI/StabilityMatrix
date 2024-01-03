using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallExtensionStep(ExtensionBase extension, DirectoryPath extensionsDir) : IPackageStep
{
    public Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        return extension.InstallExtensionAsync(extensionsDir, extension.MainBranch);
    }

    public string ProgressTitle => $"Installing {extension.DisplayName}";
}
