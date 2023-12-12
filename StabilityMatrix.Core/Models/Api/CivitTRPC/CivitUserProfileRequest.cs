using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

public record CivitUserProfileRequest : IFormattable
{
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("authed")]
    public bool Authed { get; set; }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return JsonSerializer.Serialize(new { json = this });
    }
}
