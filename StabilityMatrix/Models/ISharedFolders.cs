namespace StabilityMatrix.Models;

public interface ISharedFolders
{
    string SharedFolderTypeToName(SharedFolderType folderType);
    void SetupLinksForPackage(BasePackage basePackage, string installPath);
}
