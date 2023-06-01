using System.Diagnostics;
using System.Threading.Tasks;

namespace StabilityMatrix.Helper;

public interface IPrerequisiteHelper
{
    Task<Process?> InstallGitIfNecessary();
}