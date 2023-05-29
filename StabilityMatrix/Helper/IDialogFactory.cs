using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public interface IDialogFactory
{
    LaunchOptionsDialog CreateLaunchOptionsDialog(BasePackage selectedPackage);
}
