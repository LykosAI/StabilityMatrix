using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Core.Updater;

public interface IUpdateHelper
{
    Task StartCheckingForUpdates();

    Task DownloadUpdate(UpdateInfo updateInfo,
        IProgress<ProgressReport> progress);
}
