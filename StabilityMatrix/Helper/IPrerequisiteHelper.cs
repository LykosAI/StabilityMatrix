using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IPrerequisiteHelper
{
    Task InstallGitIfNecessary(IProgress<ProgressReport>? progress = null);
}
