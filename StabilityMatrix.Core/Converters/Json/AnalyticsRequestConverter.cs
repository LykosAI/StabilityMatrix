using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;

namespace StabilityMatrix.Core.Converters.Json;

public class AnalyticsRequestConverter : JsonConverter<AnalyticsRequest>
{
    public override AnalyticsRequest? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var root = jsonDocument.RootElement;

        if (root.TryGetProperty("Type", out var typeProperty))
        {
            var type = typeProperty.GetString();
            return type switch
            {
                "package-install"
                    => JsonSerializer.Deserialize<PackageInstallAnalyticsRequest>(root.GetRawText(), options),
                "first-time-install"
                    => JsonSerializer.Deserialize<FirstTimeInstallAnalytics>(root.GetRawText(), options),
                "launch" => JsonSerializer.Deserialize<LaunchAnalyticsRequest>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown Type: {type}")
            };
        }

        throw new JsonException("Missing Type property");
    }

    public override void Write(Utf8JsonWriter writer, AnalyticsRequest value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
