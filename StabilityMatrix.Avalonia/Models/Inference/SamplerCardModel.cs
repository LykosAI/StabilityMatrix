using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(SamplerCardModel))]
public class SamplerCardModel
{
    public int Steps { get; set; }
    public double CfgScale { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? SelectedSampler { get; set; }
}
