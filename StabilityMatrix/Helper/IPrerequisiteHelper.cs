using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IPrerequisiteHelper
{
    Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null);
    Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null);

    Task SetupPythonDependencies(string installLocation, string requirementsFileName,
        IProgress<ProgressReport>? progress = null, Action<string?>? onConsoleOutput = null);
}
