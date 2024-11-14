namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public class OpenModelDbResource
{
    public string? Platform { get; set; }
    public string? Type { get; set; }
    public long Size { get; set; }
    public string? Sha256 { get; set; }
    public List<string>? Urls { get; set; }
}
