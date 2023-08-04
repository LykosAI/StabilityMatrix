using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(SeedCardModel))]
public class SeedCardModel
{
    public string? Seed { get; set; }
    public bool IsRandomizeEnabled { get; set; }
}
