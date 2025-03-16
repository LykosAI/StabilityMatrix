using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Models.PackageModification;

public class PipStep : IPackageStep
{
    public required ProcessArgs Args { get; init; }
    public required DirectoryPath VenvDirectory { get; init; }

    public DirectoryPath? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

    public PyBaseInstall? BaseInstall { get; set; }

    /// <inheritdoc />
    public string ProgressTitle =>
        Args switch
        {
            _ when Args.Contains("install") => "Installing Pip Packages",
            _ when Args.Contains("uninstall") => "Uninstalling Pip Packages",
            _ when Args.Contains("-U") || Args.Contains("--upgrade") => "Updating Pip Packages",
            _ => "Running Pip"
        };

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        BaseInstall ??= PyBaseInstall.Default;
        await using var venvRunner = BaseInstall.CreateVenvRunner(
            VenvDirectory,
            workingDirectory: WorkingDirectory,
            environmentVariables: EnvironmentVariables
        );

        venvRunner.RunDetached(Args.Prepend(["-m", "pip"]), progress.AsProcessOutputHandler());

        await ProcessRunner.WaitForExitConditionAsync(venvRunner.Process).ConfigureAwait(false);
    }
}
