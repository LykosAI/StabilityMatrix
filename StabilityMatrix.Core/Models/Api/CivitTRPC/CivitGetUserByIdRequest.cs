using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

public record CivitGetUserByIdRequest : IFormattable
{
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return JsonSerializer.Serialize(new { json = this });
    }
}
