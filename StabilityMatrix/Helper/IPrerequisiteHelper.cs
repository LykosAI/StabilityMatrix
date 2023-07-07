using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Helper;

public interface IPrerequisiteHelper
{
    string GitBinPath { get; }
    
    bool IsPythonInstalled { get; }
 
    Task InstallAllIfNecessary(IProgress<ProgressReport>? progress = null);
    Task UnpackResourcesIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallVcRedistIfNecessary(IProgress<ProgressReport>? progress = null);
    Task InstallPythonIfNecessary(IProgress<ProgressReport>? progress = null);

    /// <summary>
    /// Run embedded git with the given arguments.
    /// </summary>
    Task RunGit(string? workingDirectory = null, params string[] args);

    Task SetupPythonDependencies(string installLocation, string requirementsFileName,
        IProgress<ProgressReport>? progress = null, Action<string?>? onConsoleOutput = null);

    void UpdatePathExtensions();
}
