using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Models.Packages;

namespace StabilityMatrix.Models;

public interface ISharedFolders
{
    void SetupLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory);
    void UpdateLinksForPackage(BasePackage basePackage, DirectoryPath installDirectory);
    void RemoveLinksForAllPackages();
}
