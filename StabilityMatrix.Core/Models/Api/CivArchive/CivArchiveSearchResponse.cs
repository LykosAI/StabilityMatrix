using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivArchive;

public class CivArchiveSearchResponse
{
    public required IReadOnlyList<CivArchiveSearchResult> Results { get; init; }
    public required CivArchiveFilterOptions FilterOptions { get; init; }
    public required CivArchiveSearchFilters EffectiveFilters { get; init; }
    public required string CanonicalUrl { get; init; }
    public required int Hits { get; init; }
    public required int TotalHits { get; init; }
}

public class CivArchiveFilterOptions
{
    public IReadOnlyList<string> BaseModels { get; init; } = [];
    public IReadOnlyList<string> ModelTypes { get; init; } = [];
}

public class CivArchiveSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("kind")]
    public string KindRaw { get; set; } = "version";

    [JsonIgnore]
    public CivArchiveKindOption Kind => CivArchiveEnumExtensions.ParseKind(KindRaw);

    /// <summary>
    /// True when the result kind is anything other than the default <c>Version</c>
    /// (e.g. <c>File</c> or <c>User</c>). Most search results are Version-kind, so the
    /// Kind chip on the card is only worth showing when it's something the user
    /// wouldn't otherwise expect.
    /// </summary>
    [JsonIgnore]
    public bool ShouldShowKindBadge => Kind != CivArchiveKindOption.Version;

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("download_count")]
    public long DownloadCount { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("base_model")]
    public string? BaseModel { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("video_url")]
    public string? VideoUrl { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(CivArchiveUnixOrIsoDateTimeConverter))]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("deleted_at")]
    [JsonConverter(typeof(CivArchiveUnixOrIsoDateTimeConverter))]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    /// <summary>
    /// Set client-side after a search response when the result's SHA256 matches a local model file.
    /// </summary>
    [JsonIgnore]
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Set client-side alongside <see cref="IsInstalled"/> for tooltip display.
    /// </summary>
    [JsonIgnore]
    public string? LocalInstallPath { get; set; }

    [JsonIgnore]
    public string? Sha256FromUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                return null;
            }

            var marker = "/sha256/";
            var index = Url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            return Url[(index + marker.Length)..].Trim('/');
        }
    }
}

public sealed class CivArchiveUnixOrIsoDateTimeConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var unixTime))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (long.TryParse(value, out unixTime))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTime);
            }

            if (DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        throw new JsonException($"Unsupported date token {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
