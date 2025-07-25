namespace StabilityMatrix.Core.Models;

public class CivitaiResource
{
    public string Type { get; set; }
    public int ModelVersionId { get; set; }
    public string ModelName { get; set; }
    public string ModelVersionName { get; set; }
    public double? Weight { get; set; }
}
