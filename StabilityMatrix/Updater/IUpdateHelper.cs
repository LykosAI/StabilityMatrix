using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Updater;

public interface IUpdateHelper
{
    Task StartCheckingForUpdates();

    Task DownloadUpdate(UpdateInfo updateInfo,
        IProgress<ProgressReport> progress);
}
