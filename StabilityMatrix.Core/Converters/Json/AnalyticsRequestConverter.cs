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

        if (
            !root.TryGetProperty("type", out var typeProperty)
            && !root.TryGetProperty("Type", out typeProperty)
        )
            throw new JsonException("Missing type property");

        var type = typeProperty.GetString();
        return type switch
        {
            "package-install" => JsonSerializer.Deserialize<PackageInstallAnalyticsRequest>(
                root.GetRawText(),
                options
            ),
            "first-time-install" => JsonSerializer.Deserialize<FirstTimeInstallAnalytics>(
                root.GetRawText(),
                options
            ),
            "launch" => JsonSerializer.Deserialize<LaunchAnalyticsRequest>(root.GetRawText(), options),
            "hardware" => JsonSerializer.Deserialize<HardwareAnalyticsRequest>(root.GetRawText(), options),
            "package-usage" => JsonSerializer.Deserialize<PackageUsageAnalyticsRequest>(
                root.GetRawText(),
                options
            ),
            "inference-usage" => JsonSerializer.Deserialize<InferenceUsageAnalyticsRequest>(
                root.GetRawText(),
                options
            ),
            "feature-usage" => JsonSerializer.Deserialize<FeatureUsageAnalyticsRequest>(
                root.GetRawText(),
                options
            ),
            _ => throw new JsonException($"Unknown Type: {type}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, AnalyticsRequest value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
