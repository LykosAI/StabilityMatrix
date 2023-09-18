using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class PackageModificationRunner : IPackageModificationRunner
{
    public async Task ExecuteSteps(IReadOnlyList<IPackageStep> steps)
    {
        IProgress<ProgressReport> progress = new Progress<ProgressReport>(report =>
        {
            CurrentProgress = report;
            if (!string.IsNullOrWhiteSpace(report.Message))
            {
                ConsoleOutput.Add(report.Message);
            }

            OnProgressChanged(report);
        });

        IsRunning = true;
        foreach (var step in steps)
        {
            CurrentStep = step;
            try
            {
                await step.ExecuteAsync(progress).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                progress.Report(
                    new ProgressReport(
                        1f,
                        title: "Error modifying package",
                        message: $"Error: {e}",
                        isIndeterminate: false
                    )
                );
                Failed = true;
                break;
            }
        }

        if (!Failed)
        {
            progress.Report(
                new ProgressReport(
                    1f,
                    message: ModificationCompleteMessage ?? "Package Install Complete",
                    isIndeterminate: false
                )
            );
        }

        IsRunning = false;
    }

    public string? ModificationCompleteMessage { get; init; }
    public bool ShowDialogOnStart { get; init; }

    public bool IsRunning { get; set; }
    public bool Failed { get; set; }
    public ProgressReport CurrentProgress { get; set; }
    public IPackageStep? CurrentStep { get; set; }
    public List<string> ConsoleOutput { get; } = new();
    public Guid Id { get; } = Guid.NewGuid();

    public event EventHandler<ProgressReport>? ProgressChanged;

    protected virtual void OnProgressChanged(ProgressReport e) => ProgressChanged?.Invoke(this, e);
}
