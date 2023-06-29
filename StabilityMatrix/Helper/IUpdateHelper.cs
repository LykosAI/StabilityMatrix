using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IUpdateHelper
{
    Task StartCheckingForUpdates();

    Task DownloadUpdate(UpdateInfo updateInfo,
        IProgress<ProgressReport> progress);
}
