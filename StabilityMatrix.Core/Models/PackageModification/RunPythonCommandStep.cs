using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class RunPythonCommandStep(
    IPyInstallationManager pyInstallationManager,
    ISettingsManager settingsManager
) : IPackageStep
{
    public required InstalledPackage InstalledPackage { get; init; }
    public required DirectoryPath WorkingDirectory { get; init; }
    public required ProcessArgs Arguments { get; init; }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(
            new ProgressReport(-1f, message: "Setting up virtual environment...", isIndeterminate: true)
        );

        var venvDir = WorkingDirectory.JoinDir("venv");
        var pyVersion = PyVersion.Parse(InstalledPackage.PythonVersion);
        if (pyVersion.StringValue == "0.0.0")
        {
            pyVersion = PyInstallationManager.Python_3_10_11;
        }

        var baseInstall = !string.IsNullOrWhiteSpace(InstalledPackage.PythonVersion)
            ? new PyBaseInstall(
                await pyInstallationManager.GetInstallationAsync(pyVersion).ConfigureAwait(false)
            )
            : PyBaseInstall.Default;

        await using var venvRunner = baseInstall.CreateVenvRunner(
            venvDir,
            workingDirectory: WorkingDirectory,
            environmentVariables: settingsManager.Settings.EnvironmentVariables
        );

        venvRunner.RunDetached(Arguments, progress.AsProcessOutputHandler());
        if (venvRunner.Process != null)
        {
            await venvRunner.Process.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    public string ProgressTitle => "Running Python Command";
}
