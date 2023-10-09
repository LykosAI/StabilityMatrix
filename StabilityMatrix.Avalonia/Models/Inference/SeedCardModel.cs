using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(SeedCardModel))]
public record SeedCardModel
{
    [JsonNumberHandling(
        JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString
    )]
    public long Seed { get; init; }
    public bool IsRandomizeEnabled { get; init; }
}
