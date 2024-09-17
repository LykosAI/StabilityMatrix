namespace StabilityMatrix.Core.Models.Api.Pypi;

public class PyPiResponse
{
    public Dictionary<string, List<PyPiReleaseFile>>? Releases { get; set; }
}
