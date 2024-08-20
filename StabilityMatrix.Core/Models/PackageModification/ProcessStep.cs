using System.Collections.Immutable;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.PackageModification;

public class ProcessStep : ICancellablePackageStep
{
    public required string FileName { get; init; }

    public ProcessArgs Args { get; init; } = "";

    public DirectoryPath? WorkingDirectory { get; init; }

    public ImmutableDictionary<string, string> EnvironmentVariables { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    public bool UseAnsiParsing { get; init; } = true;

    public string ProgressTitle { get; init; } = "Running Process";

    /// <inheritdoc />
    public async Task ExecuteAsync(
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(
            new ProgressReport
            {
                Message = "Running Process",
                IsIndeterminate = true,
                PrintToConsole = true
            }
        );

        if (UseAnsiParsing)
        {
            using var process = ProcessRunner.StartAnsiProcess(
                fileName: FileName,
                arguments: Args.ToString(),
                workingDirectory: WorkingDirectory?.FullPath,
                environmentVariables: EnvironmentVariables,
                outputDataReceived: progress.AsProcessOutputHandler()
            );

            await ProcessRunner
                .WaitForExitConditionAsync(process, cancelToken: cancellationToken)
                .ConfigureAwait(false);

            await process.WaitUntilOutputEOF(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using var process = ProcessRunner.StartProcess(
                fileName: FileName,
                arguments: Args.ToString(),
                workingDirectory: WorkingDirectory?.FullPath,
                environmentVariables: EnvironmentVariables,
                outputDataReceived: progress is null
                    ? null
                    : output =>
                    {
                        progress.Report(
                            new ProgressReport
                            {
                                Message = output,
                                IsIndeterminate = true,
                                PrintToConsole = true
                            }
                        );
                    }
            );

            await ProcessRunner
                .WaitForExitConditionAsync(process, cancelToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
