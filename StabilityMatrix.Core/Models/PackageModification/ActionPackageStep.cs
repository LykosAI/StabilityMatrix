using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

/// <summary>
/// A package step that wraps an async action, useful for ad-hoc operations
/// that need to run within the PackageModificationRunner.
/// </summary>
public class ActionPackageStep(
    Func<IProgress<ProgressReport>, Task> action,
    string progressTitle = "Working..."
) : IPackageStep
{
    public string ProgressTitle => progressTitle;

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress)
    {
        await action(progress ?? new Progress<ProgressReport>()).ConfigureAwait(false);
    }
}
