using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class PackageModificationRunner : IPackageModificationRunner
{
    public async Task ExecuteSteps(IEnumerable<IPackageStep> steps)
    {
        IProgress<ProgressReport> progress = new Progress<ProgressReport>(report =>
        {
            CurrentProgress = report;
            if (!string.IsNullOrWhiteSpace(report.Message) && report.PrintToConsole)
            {
                ConsoleOutput.Add(report.Message);
            }

            if (!string.IsNullOrWhiteSpace(report.Title) && report.PrintToConsole)
            {
                ConsoleOutput.Add(report.Title);
            }

            OnProgressChanged(report);
        });

        IsRunning = true;

        try
        {
            foreach (var step in steps)
            {
                CurrentStep = step;
                try
                {
                    await step.ExecuteAsync(progress).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    var failedMessage = string.IsNullOrWhiteSpace(ModificationFailedMessage)
                        ? $"Error: {e}"
                        : ModificationFailedMessage + $" ({e})";

                    progress.Report(
                        new ProgressReport(
                            1f,
                            title: ModificationFailedTitle,
                            message: failedMessage,
                            isIndeterminate: false
                        )
                    );

                    Exception = e;
                    Failed = true;
                    return;
                }
            }

            if (!Failed)
            {
                progress.Report(
                    new ProgressReport(
                        1f,
                        title: ModificationCompleteTitle,
                        message: ModificationCompleteMessage,
                        isIndeterminate: false
                    )
                );
            }
        }
        finally
        {
            IsRunning = false;
            OnCompleted();
        }
    }

    public bool HideCloseButton { get; init; }

    public bool CloseWhenFinished { get; init; } = true;

    public bool ShowDialogOnStart { get; init; }

    public string? ModificationCompleteTitle { get; init; } = "Install Complete";

    public required string ModificationCompleteMessage { get; init; }

    public string? ModificationFailedTitle { get; init; } = "Install Failed";

    public string? ModificationFailedMessage { get; init; }

    public bool IsRunning { get; private set; }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Failed { get; private set; }

    public Exception? Exception { get; set; }

    public ProgressReport CurrentProgress { get; set; }

    public IPackageStep? CurrentStep { get; set; }

    public List<string> ConsoleOutput { get; } = new();

    public Guid Id { get; } = Guid.NewGuid();

    public event EventHandler<ProgressReport>? ProgressChanged;

    public event EventHandler<IPackageModificationRunner>? Completed;

    protected virtual void OnProgressChanged(ProgressReport e) => ProgressChanged?.Invoke(this, e);

    protected virtual void OnCompleted() => Completed?.Invoke(this, this);
}
