namespace StabilityMatrix.Core.Exceptions;

public class FileTransferExistsException : IOException
{
    public string SourceFile { get; }
    public string DestinationFile { get; }

    public FileTransferExistsException(string source, string destination)
    {
        SourceFile = source;
        DestinationFile = destination;
    }
}
