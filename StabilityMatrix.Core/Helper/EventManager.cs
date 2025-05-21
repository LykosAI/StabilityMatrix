using System.Globalization;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Core.Helper;

public record struct RunningPackageStatusChangedEventArgs(PackagePair? CurrentPackagePair);

public class EventManager
{
    public static EventManager Instance { get; } = new();

    private EventManager() { }

    public event EventHandler<int>? GlobalProgressChanged;
    public event EventHandler? InstalledPackagesChanged;
    public event EventHandler<bool>? OneClickInstallFinished;
    public event EventHandler? TeachingTooltipNeeded;
    public event EventHandler<bool>? DevModeSettingChanged;
    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler<Guid>? PackageLaunchRequested;
    public event EventHandler<InstalledPackage>? PackageRelaunchRequested;
    public event EventHandler? ScrollToBottomRequested;
    public event EventHandler<ProgressItem>? ProgressChanged;
    public event EventHandler<RunningPackageStatusChangedEventArgs>? RunningPackageStatusChanged;
    public event EventHandler<IPackageModificationRunner>? PackageInstallProgressAdded;
    public delegate Task AddPackageInstallEventHandler(
        object? sender,
        IPackageModificationRunner runner,
        IReadOnlyList<IPackageStep> steps,
        Action onCompleted
    );
    public event EventHandler? ToggleProgressFlyout;
    public event EventHandler<CultureInfo>? CultureChanged;
    public event EventHandler? ModelIndexChanged;
    public event EventHandler<FilePath>? ImageFileAdded;

    public delegate Task InferenceProjectRequestedEventHandler(
        object? sender,
        LocalImageFile imageFile,
        InferenceProjectType type
    );
    public event InferenceProjectRequestedEventHandler? InferenceProjectRequested;
    public event EventHandler<InferenceQueueCustomPromptEventArgs>? InferenceQueueCustomPrompt;
    public event EventHandler<int>? NavigateAndFindCivitModelRequested;
    public event EventHandler<string?>? NavigateAndFindCivitAuthorRequested;
    public event EventHandler? DownloadsTeachingTipRequested;
    public event EventHandler? RecommendedModelsDialogClosed;
    public event EventHandler? WorkflowInstalled;
    public event EventHandler<string>? DeleteModelRequested;
    public event EventHandler? RefreshPackageListRequested;

    public void OnGlobalProgressChanged(int progress) => GlobalProgressChanged?.Invoke(this, progress);

    public void OnInstalledPackagesChanged() => InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);

    public void OnOneClickInstallFinished(bool skipped) => OneClickInstallFinished?.Invoke(this, skipped);

    public void OnTeachingTooltipNeeded() => TeachingTooltipNeeded?.Invoke(this, EventArgs.Empty);

    public void OnDevModeSettingChanged(bool value) => DevModeSettingChanged?.Invoke(this, value);

    public void OnUpdateAvailable(UpdateInfo args) => UpdateAvailable?.Invoke(this, args);

    public void OnPackageLaunchRequested(Guid packageId) => PackageLaunchRequested?.Invoke(this, packageId);

    public void OnScrollToBottomRequested() => ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);

    public void OnProgressChanged(ProgressItem progress) => ProgressChanged?.Invoke(this, progress);

    public void OnRunningPackageStatusChanged(PackagePair? currentPackagePair) =>
        RunningPackageStatusChanged?.Invoke(
            this,
            new RunningPackageStatusChangedEventArgs(currentPackagePair)
        );

    public void OnPackageInstallProgressAdded(IPackageModificationRunner runner) =>
        PackageInstallProgressAdded?.Invoke(this, runner);

    public void OnToggleProgressFlyout() => ToggleProgressFlyout?.Invoke(this, EventArgs.Empty);

    public void OnCultureChanged(CultureInfo culture) => CultureChanged?.Invoke(this, culture);

    public void OnModelIndexChanged() => ModelIndexChanged?.Invoke(this, EventArgs.Empty);

    public void OnImageFileAdded(FilePath filePath) => ImageFileAdded?.Invoke(this, filePath);

    public void OnInferenceQueueCustomPrompt(InferenceQueueCustomPromptEventArgs e) =>
        InferenceQueueCustomPrompt?.Invoke(this, e);

    public void OnNavigateAndFindCivitModelRequested(int modelId) =>
        NavigateAndFindCivitModelRequested?.Invoke(this, modelId);

    public void OnDownloadsTeachingTipRequested() =>
        DownloadsTeachingTipRequested?.Invoke(this, EventArgs.Empty);

    public void OnRecommendedModelsDialogClosed() =>
        RecommendedModelsDialogClosed?.Invoke(this, EventArgs.Empty);

    public void OnPackageRelaunchRequested(InstalledPackage package) =>
        PackageRelaunchRequested?.Invoke(this, package);

    public void OnWorkflowInstalled() => WorkflowInstalled?.Invoke(this, EventArgs.Empty);

    public void OnDeleteModelRequested(object? sender, string relativePath) =>
        DeleteModelRequested?.Invoke(sender, relativePath);

    public void OnInferenceProjectRequested(LocalImageFile imageFile, InferenceProjectType type) =>
        InferenceProjectRequested?.Invoke(this, imageFile, type);

    public void OnNavigateAndFindCivitAuthorRequested(string? author) =>
        NavigateAndFindCivitAuthorRequested?.Invoke(this, author);

    public void OnRefreshPackageListRequested() => RefreshPackageListRequested?.Invoke(this, EventArgs.Empty);
}
