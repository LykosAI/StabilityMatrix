using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StabilityMatrix.Models.Packages;

public class LinkedFolderSharedFolderStrategy : ISharedFolderStrategy
{
    private readonly IServiceProvider serviceProvider;

    public LinkedFolderSharedFolderStrategy(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }
    
    public Task ExecuteAsync(BasePackage package)
    {
        // TODO: Move SharedFolders logic here
        // NOTE: We're using this awkward solution because a circular dependency is generated in the graph otherwise
        var sharedFolders = serviceProvider.GetRequiredService<ISharedFolders>();
        sharedFolders.SetupLinksForPackage(package, package.InstallLocation);
        return Task.CompletedTask;
    }
}