namespace StabilityMatrix.Avalonia.Models.HuggingFace;

public class HuggingfaceItem
{
    public required HuggingFaceModelType ModelCategory { get; set; }
    public required string ModelName { get; set; }
    public required string RepositoryPath { get; set; }
    public required string[] Files { get; set; }
    public required string LicenseType { get; set; }
    public string? LicensePath { get; set; }
    public string? Subfolder { get; set; }
    public bool LoginRequired { get; set; }
}
