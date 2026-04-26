using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Models.Api.CivArchive;

public class CivArchiveModelDetailsResponse
{
    public required CivArchiveModelDetails Model { get; init; }
}

public class CivArchiveModelDetails
{
    [JsonPropertyName("id")]
    public JsonNodeIdWrapper? IdWrapper { get; set; }

    [JsonIgnore]
    public string Id => IdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }

    [JsonPropertyName("favoriteCount")]
    public long FavoriteCount { get; set; }

    [JsonPropertyName("commentCount")]
    public long CommentCount { get; set; }

    [JsonPropertyName("ratingCount")]
    public long RatingCount { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    [JsonPropertyName("creator_username")]
    public string? CreatorUsername { get; set; }

    [JsonPropertyName("creator_name")]
    public string? CreatorName { get; set; }

    [JsonPropertyName("creator_url")]
    public string? CreatorUrl { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("platform_name")]
    public string? PlatformName { get; set; }

    [JsonPropertyName("versions")]
    public IReadOnlyList<CivArchiveVersionReference> Versions { get; set; } = [];

    [JsonPropertyName("version")]
    public CivArchiveModelVersion? Version { get; set; }

    [JsonPropertyName("meta")]
    public CivArchiveModelMeta? Meta { get; set; }
}

public class CivArchiveVersionReference
{
    [JsonPropertyName("id")]
    public JsonNodeIdWrapper? IdWrapper { get; set; }

    [JsonIgnore]
    public string Id => IdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}

public class CivArchiveModelVersion
{
    [JsonPropertyName("id")]
    public JsonNodeIdWrapper? IdWrapper { get; set; }

    [JsonIgnore]
    public string Id => IdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("modelId")]
    public JsonNodeIdWrapper? ModelIdWrapper { get; set; }

    [JsonIgnore]
    public string ModelId => ModelIdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("baseModel")]
    public string? BaseModel { get; set; }

    [JsonPropertyName("baseModelType")]
    public string? BaseModelType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }

    [JsonPropertyName("favoriteCount")]
    public long FavoriteCount { get; set; }

    [JsonPropertyName("ratingCount")]
    public long RatingCount { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("files")]
    public IReadOnlyList<CivArchiveModelFile> Files { get; set; } = [];

    [JsonPropertyName("images")]
    public IReadOnlyList<CivArchiveModelImage> Images { get; set; } = [];

    [JsonPropertyName("trigger")]
    public IReadOnlyList<string> Trigger { get; set; } = [];

    [JsonPropertyName("allow_download")]
    public bool AllowDownload { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("platform_url")]
    public string? PlatformUrl { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("mirrors")]
    public IReadOnlyList<CivArchiveVersionMirror> Mirrors { get; set; } = [];
}

public class CivArchiveModelFile
{
    [JsonPropertyName("id")]
    public JsonNodeIdWrapper? IdWrapper { get; set; }

    [JsonIgnore]
    public string Id => IdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sizeKB")]
    public double SizeKb { get; set; }

    [JsonIgnore]
    public string FormattedSize => new FileSizeType(SizeKb).HumanReadableRepresentation;

    /// <summary>
    /// True when the API reported a plausible file size. Some non-CivitAI mirrors return
    /// a tiny placeholder (e.g. 0.01 KB) when only the SHA256 was captured — for those we
    /// hide the size chip rather than showing nonsense.
    /// </summary>
    [JsonIgnore]
    public bool HasKnownSize => SizeKb >= 1.0;

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("modelId")]
    public JsonNodeIdWrapper? ModelIdWrapper { get; set; }

    [JsonIgnore]
    public string ModelId => ModelIdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    [JsonPropertyName("modelVersionId")]
    public JsonNodeIdWrapper? ModelVersionIdWrapper { get; set; }

    [JsonIgnore]
    public string ModelVersionId => ModelVersionIdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; set; }

    [JsonPropertyName("mirrors")]
    public IReadOnlyList<CivArchiveFileMirror> Mirrors { get; set; } = [];
}

public class CivArchiveFileMirror
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("is_gated")]
    public bool IsGated { get; set; }

    [JsonPropertyName("is_paid")]
    public bool IsPaid { get; set; }
}

public class CivArchiveModelImage
{
    [JsonPropertyName("id")]
    public JsonNodeIdWrapper? IdWrapper { get; set; }

    [JsonIgnore]
    public string Id => IdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class CivArchiveVersionMirror
{
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("platform_url")]
    public string? PlatformUrl { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version_name")]
    public string? VersionName { get; set; }

    [JsonPropertyName("id")]
    public JsonNodeIdWrapper? IdWrapper { get; set; }

    [JsonIgnore]
    public string Id => IdWrapper?.ToString() ?? string.Empty;

    [JsonPropertyName("version_id")]
    public JsonNodeIdWrapper? VersionIdWrapper { get; set; }

    [JsonIgnore]
    public string VersionId => VersionIdWrapper?.ToString() ?? string.Empty;
}

public class CivArchiveModelMeta
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("canonical")]
    public string? Canonical { get; set; }
}
