using StabilityMatrix.Models.Packages;

namespace StabilityMatrix.Models;

public interface ISharedFolders
{
    void SetupLinksForPackage(BasePackage basePackage, string installPath);
    void UpdateLinksForPackage(BasePackage basePackage, string installPath);
}
