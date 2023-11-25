using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

public record CivitUserToggleFavoriteModelRequest : IFormattable
{
    [JsonPropertyName("modelId")]
    public required int ModelId { get; set; }

    [JsonPropertyName("authed")]
    public bool Authed { get; set; } = true;

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return JsonSerializer.Serialize(new { json = this });
    }
}
