using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
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
        ServiceManager<ViewModelBase> dialogFactory
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

    protected override Task LaunchImpl()
    {
        IsLaunchTeachingTipsOpen = false;

        RunningPackage = new DankDiffusion(null!, null!, null!, null!);
        
        return Task.CompletedTask;
    }

    public override Task Stop()
    {
        RunningPackage = null;
        
        return Task.CompletedTask;
    }
}
