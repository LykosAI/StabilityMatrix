using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Avalonia.Models.PackageSteps;

public class UnpackSiteCustomizeStep(DirectoryPath venvPath) : IPackageStep
{
    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        var sitePackages = venvPath.JoinDir(PyVenvRunner.RelativeSitePackagesPath);
        var file = sitePackages.JoinFile("sitecustomize.py");
        file.Directory?.Create();
        await Assets.PyScriptSiteCustomize.ExtractTo(file);
    }

    public string ProgressTitle => "Unpacking prerequisites...";
}
