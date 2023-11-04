using System.Runtime.Versioning;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Helper;

public interface IPrerequisiteHelper
{
    string GitBinPath { get; }

    bool IsPythonInstalled { get; }

    Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null);
    Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null);

    [SupportedOSPlatform("Windows")]
    Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null);

    /// <summary>
    /// Run embedded git with the given arguments.
    /// </summary>
    Task RunGit(
        string? workingDirectory = null,
        Action<ProcessOutput>? onProcessOutput = null,
        params string[] args
    );
    Task<string> GetGitOutput(string? workingDirectory = null, params string[] args);
    Task InstallTkinterIfNecessary(IProgress<ProgressReport>? progress = null);
}
