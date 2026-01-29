using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockLaunchPageViewModel : LaunchPageViewModel
{
    public MockLaunchPageViewModel(
        ILogger<MockLaunchPageViewModel> logger,
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IPyRunner pyRunner,
        INotificationService notificationService,
        ISharedFolders sharedFolders,
        IServiceManager<ViewModelBase> dialogFactory
    )
        : base(
            logger,
            settingsManager,
            packageFactory,
            pyRunner,
            notificationService,
            sharedFolders,
            dialogFactory
        ) { }

    public override BasePackage? SelectedBasePackage =>
        SelectedPackage?.PackageName != "dank-diffusion"
            ? base.SelectedBasePackage
            : new DankDiffusion(null!, null!, null!, null!, null!, null!);

    protected override Task LaunchImpl(string? command)
    {
        IsLaunchTeachingTipsOpen = false;

        RunningPackage = new PackagePair(null!, new DankDiffusion(null!, null!, null!, null!, null!, null!));

        Console.Document.Insert(
            0,
            """
            Python 3.10.11 (tags/v3.10.11:7d4cc5a, Apr  5 2023, 00:38:17) [MSC v.1929 64 bit (AMD64)]
            Version: 1.5.0
            Commit hash: <none>

            Fetching updates for midas...
            Checking out commit for midas with hash: 2e42b7f...
            """
        );

        return Task.CompletedTask;
    }

    public override Task Stop()
    {
        RunningPackage = null;

        return Task.CompletedTask;
    }
}
