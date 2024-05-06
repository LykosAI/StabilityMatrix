namespace StabilityMatrix.Core.Models.Database;

public class LocalModelFolder
{
    public required string RelativePath { get; set; }

    public Dictionary<string, LocalModelFile> Files { get; set; } = [];

    public Dictionary<string, LocalModelFolder> Folders { get; set; } = [];
}
