using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class PackageModificationRunner : IPackageModificationRunner
{
    public async Task ExecuteSteps(IReadOnlyList<IPackageStep> steps)
    {
        var progress = new Progress<ProgressReport>(report =>
        {
            CurrentProgress = report;
            OnProgressChanged(report);
        });

        IsRunning = true;
        foreach (var step in steps)
        {
            CurrentStep = step;
            await step.ExecuteAsync(progress).ConfigureAwait(false);
        }

        IsRunning = false;
    }

    public bool IsRunning { get; set; }
    public ProgressReport CurrentProgress { get; set; }
    public IPackageStep? CurrentStep { get; set; }

    public event EventHandler<ProgressReport>? ProgressChanged;

    protected virtual void OnProgressChanged(ProgressReport e) => ProgressChanged?.Invoke(this, e);
}
