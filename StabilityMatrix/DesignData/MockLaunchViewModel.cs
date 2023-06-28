using System.ComponentModel;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Helper;
using StabilityMatrix.Python;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;

namespace StabilityMatrix.DesignData;

[DesignOnly(true)]
public class MockLaunchViewModel : LaunchViewModel
{
    public MockLaunchViewModel() : base(
        null!, null!, null!, 
        null!, null!, null!,
        null!, null!, null!)
    {
        InstalledPackages = new()
        {
            new()
            {
                DisplayName = "Mock Package",
                PackageName = "mock-package",
                PackageVersion = "1.0.0",
                DisplayVersion = "1.0.0",
                InstalledBranch = "main",
                LibraryPath = @"Packages\mock-package",
            }
        };
        SelectedPackage = InstalledPackages[0];
    }
}
