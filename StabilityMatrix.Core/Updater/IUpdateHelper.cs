using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Core.Updater;

public interface IUpdateHelper
{
    event EventHandler<UpdateStatusChangedEventArgs>? UpdateStatusChanged;

    Task StartCheckingForUpdates();

    Task CheckForUpdate();

    Task DownloadUpdate(UpdateInfo updateInfo, IProgress<ProgressReport> progress);
}
