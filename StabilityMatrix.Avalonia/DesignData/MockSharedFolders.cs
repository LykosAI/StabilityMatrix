using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockSharedFolders : ISharedFolders
{
    public void SetupLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory)
    {
    }

    public void UpdateLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory)
    {
    }

    public void RemoveLinksForAllPackages()
    {
    }
}
