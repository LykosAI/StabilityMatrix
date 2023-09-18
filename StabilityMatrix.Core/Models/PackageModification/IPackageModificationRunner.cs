using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public interface IPackageModificationRunner
{
    Task ExecuteSteps(IReadOnlyList<IPackageStep> steps);
    bool IsRunning { get; set; }
    ProgressReport CurrentProgress { get; set; }
    IPackageStep? CurrentStep { get; set; }
    event EventHandler<ProgressReport>? ProgressChanged;
    List<string> ConsoleOutput { get; }
    Guid Id { get; }
    bool ShowDialogOnStart { get; init; }
    string? ModificationCompleteMessage { get; init; }
    bool Failed { get; set; }
}
