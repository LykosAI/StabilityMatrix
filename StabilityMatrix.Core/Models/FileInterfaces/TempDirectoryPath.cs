namespace StabilityMatrix.Core.Models.FileInterfaces;

public class TempDirectoryPath : DirectoryPath, IDisposable
{
    public TempDirectoryPath() : base(Path.GetTempPath(), Path.GetRandomFileName())
    {
        Directory.CreateDirectory(FullPath);
    }

    public void Dispose()
    {
        Directory.Delete(FullPath, true);
        GC.SuppressFinalize(this);
    }
}
