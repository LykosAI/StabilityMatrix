using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Core.Helper;

public record struct RunningPackageStatusChangedEventArgs(PackagePair? CurrentPackagePair);

public class EventManager
{
    public static EventManager Instance { get; } = new();

    private EventManager()
    {

    }
    
    public event EventHandler<int>? GlobalProgressChanged;
    public event EventHandler<Type>? PageChangeRequested;
    public event EventHandler? InstalledPackagesChanged;
    public event EventHandler<bool>? OneClickInstallFinished;
    public event EventHandler? TeachingTooltipNeeded;
    public event EventHandler<bool>? DevModeSettingChanged;
    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler<Guid> PackageLaunchRequested;
    public event EventHandler? ScrollToBottomRequested;
    public event EventHandler<ProgressItem>? ProgressChanged;
    public event EventHandler<RunningPackageStatusChangedEventArgs>? RunningPackageStatusChanged; 
    
    public void OnGlobalProgressChanged(int progress) => GlobalProgressChanged?.Invoke(this, progress);
    public void RequestPageChange(Type pageType) => PageChangeRequested?.Invoke(this, pageType);
    public void OnInstalledPackagesChanged() => InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
    public void OnOneClickInstallFinished(bool skipped) => OneClickInstallFinished?.Invoke(this, skipped);
    public void OnTeachingTooltipNeeded() => TeachingTooltipNeeded?.Invoke(this, EventArgs.Empty);
    public void OnDevModeSettingChanged(bool value) => DevModeSettingChanged?.Invoke(this, value);
    public void OnUpdateAvailable(UpdateInfo args) => UpdateAvailable?.Invoke(this, args);
    public void OnPackageLaunchRequested(Guid packageId) =>
        PackageLaunchRequested?.Invoke(this, packageId);
    public void OnScrollToBottomRequested() =>
        ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
    public void OnProgressChanged(ProgressItem progress) =>
        ProgressChanged?.Invoke(this, progress);
    public void OnRunningPackageStatusChanged(PackagePair? currentPackagePair) =>
        RunningPackageStatusChanged?.Invoke(this, new RunningPackageStatusChangedEventArgs(currentPackagePair));
}
