namespace StabilityMatrix.Models;

public interface ISharedFolders
{
    string SharedFoldersPath { get; }
    string SharedFolderTypeToName(SharedFolderType folderType);
    void SetupLinksForPackage(BasePackage basePackage, string installPath);
}
