using System.Diagnostics.CodeAnalysis;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public interface IPackageModificationRunner
{
    Task ExecuteSteps(IEnumerable<IPackageStep> steps);

    bool IsRunning { get; }

    [MemberNotNullWhen(true, nameof(Exception))]
    bool Failed { get; }

    Exception? Exception { get; }

    ProgressReport CurrentProgress { get; set; }

    IPackageStep? CurrentStep { get; set; }

    event EventHandler<ProgressReport>? ProgressChanged;

    event EventHandler<IPackageModificationRunner>? Completed;

    List<string> ConsoleOutput { get; }

    Guid Id { get; }

    bool ShowDialogOnStart { get; init; }

    bool HideCloseButton { get; init; }

    bool CloseWhenFinished { get; init; }

    string? ModificationCompleteTitle { get; init; }

    string ModificationCompleteMessage { get; init; }

    string? ModificationFailedTitle { get; init; }

    string? ModificationFailedMessage { get; init; }
}
