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
            await step.ExecuteAsync(progress).ConfigureAwait(false);
        }

        progress.Report(
            new ProgressReport(1f, message: "Package Install Complete", isIndeterminate: false)
        );

        IsRunning = false;
    }

    public bool IsRunning { get; set; }
    public ProgressReport CurrentProgress { get; set; }
    public IPackageStep? CurrentStep { get; set; }
    public List<string> ConsoleOutput { get; } = new();
    public Guid Id { get; } = Guid.NewGuid();

    public event EventHandler<ProgressReport>? ProgressChanged;

    protected virtual void OnProgressChanged(ProgressReport e) => ProgressChanged?.Invoke(this, e);
}
