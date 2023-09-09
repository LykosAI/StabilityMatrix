using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public interface IPackageStep
{
    Task ExecuteAsync(IProgress<ProgressReport>? progress = null);
    string ProgressTitle { get; }
}
