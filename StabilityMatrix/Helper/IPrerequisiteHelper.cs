using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IPrerequisiteHelper
{
    event EventHandler<ProgressReport>? DownloadProgressChanged;
    event EventHandler<ProgressReport>? DownloadComplete;
    event EventHandler<ProgressReport>? InstallProgressChanged;
    event EventHandler<ProgressReport>? InstallComplete;
    Task InstallGitIfNecessary();
}
