namespace StabilityMatrix.Core.Exceptions;

public class MissingPrerequisiteException(
    string missingPrereqName,
    string message,
    string? downloadLink = null
) : Exception($"{message}{Environment.NewLine}{downloadLink}{Environment.NewLine}")
{
    public string MissingPrereqName { get; set; } = missingPrereqName;
    public string? DownloadLink { get; set; } = downloadLink;
}
