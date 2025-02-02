using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Models.Packages;

public static class PackageHelper
{
    /// <summary>
    /// Get all BasePackage implementations in the assembly.
    /// </summary>
    public static IEnumerable<BasePackage> GetPackages()
    {
        var services = new ServiceCollection();
        services
            .AddSingleton(Substitute.For<IGithubApiCache>())
            .AddSingleton(Substitute.For<ISettingsManager>())
            .AddSingleton(Substitute.For<IDownloadService>())
            .AddSingleton(Substitute.For<IPyRunner>())
            .AddSingleton(Substitute.For<IPrerequisiteHelper>());

        var assembly = typeof(BasePackage).Assembly;
        var packageTypes = assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BasePackage)) && !t.IsAbstract)
            .Where(t => t != typeof(DankDiffusion) && t != typeof(UnknownPackage))
            .ToList();

        // Register all package types
        services.TryAddEnumerable(
            packageTypes.Select(t => ServiceDescriptor.Singleton(typeof(BasePackage), t))
        );

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetServices<BasePackage>();
    }
}
