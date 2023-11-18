namespace StabilityMatrix.Core.Models.FileInterfaces;

public partial class FilePath
{
    /// <summary>
    /// Return a new <see cref="FilePath"/> with the given file name.
    /// </summary>
    public FilePath WithName(string fileName)
    {
        if (
            Path.GetDirectoryName(FullPath) is { } directory
            && !string.IsNullOrWhiteSpace(directory)
        )
        {
            return new FilePath(directory, fileName);
        }

        return new FilePath(fileName);
    }
}
