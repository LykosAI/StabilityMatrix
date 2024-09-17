using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public interface ICancellablePackageStep : IPackageStep
{
    Task IPackageStep.ExecuteAsync(IProgress<ProgressReport>? progress)
    {
        return ExecuteAsync(progress, CancellationToken.None);
    }

    Task ExecuteAsync(
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
}
