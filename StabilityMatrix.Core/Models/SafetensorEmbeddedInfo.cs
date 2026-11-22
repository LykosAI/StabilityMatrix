using System.Globalization;
using System.Text.Json;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Model information embedded in the <c>__metadata__</c> of a SafeTensor file: the
/// LoRA-Container cover and connected model info, the ModelSpec thumbnail and trigger phrase,
/// and the base model declared by ModelSpec or kohya-ss training metadata.
/// </summary>
public record SafetensorEmbeddedInfo
{
    /// <summary>
    /// Cover image bytes (JPEG or PNG), from <c>cover_1</c> or <c>modelspec.thumbnail</c>.
    /// </summary>
    public byte[]? Cover { get; init; }

    /// <summary>
    /// Copy of the model's connected model info, from <c>cm_info</c>.
    /// </summary>
    public ConnectedModelInfo? ConnectedModelInfo { get; init; }

    /// <summary>
    /// Base model, as a CivitAI base model name (e.g. "SDXL 1.0").
    /// </summary>
    public string? BaseModel { get; init; }

    /// <summary>
    /// Trigger words, from <c>modelspec.trigger_phrase</c>.
    /// </summary>
    public string[]? TrainedWords { get; init; }

    /// <summary>
    /// Lower-case hex SHA-256 of the file before anything was embedded, from <c>model_sha256_original</c>.
    /// </summary>
    public string? OriginalSha256 { get; init; }

    public bool IsEmpty =>
        Cover is null
        && ConnectedModelInfo is null
        && BaseModel is null
        && TrainedWords is null
        && OriginalSha256 is null;

    /// <summary>
    /// Reads the embedded model information of a SafeTensor file.
    /// </summary>
    /// <returns>The embedded information, or <see langword="null"/> if the file does not contain metadata.</returns>
    public static async Task<SafetensorEmbeddedInfo?> ReadAsync(
        string safetensorPath,
        CancellationToken cancellationToken = default
    )
    {
        await using var stream = new FileStream(
            safetensorPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        var metadataDict = await SafetensorMetadata
            .ReadMetadataDictionaryAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        return metadataDict is null ? null : FromMetadata(metadataDict);
    }

    internal static SafetensorEmbeddedInfo FromMetadata(IReadOnlyDictionary<string, string> metadataDict)
    {
        return new SafetensorEmbeddedInfo
        {
            Cover = GetCover(metadataDict) ?? GetModelSpecThumbnail(metadataDict),
            ConnectedModelInfo = GetConnectedModelInfo(metadataDict),
            BaseModel = GetBaseModel(metadataDict),
            TrainedWords = GetTrainedWords(metadataDict),
            OriginalSha256 = GetOriginalSha256(metadataDict),
        };
    }

    /// <summary>
    /// Gets a LoRA-Container cover image.
    /// <c>cover_1</c> is the preview image, <c>cover_2</c> onwards are gallery images.
    /// </summary>
    /// <param name="metadataDict">The <c>__metadata__</c> entries of a SafeTensor file.</param>
    /// <param name="coverNumber">1-based number of the cover to read.</param>
    /// <returns>The image bytes, or <see langword="null"/> if the cover is missing or invalid.</returns>
    internal static byte[]? GetCover(IReadOnlyDictionary<string, string> metadataDict, int coverNumber = 1)
    {
        if (
            coverNumber < 1
            || !metadataDict.TryGetValue("cover_count", out var coverCountText)
            || !int.TryParse(
                coverCountText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var coverCount
            )
            || coverNumber > coverCount
            || !metadataDict.TryGetValue($"cover_{coverNumber}", out var coverValue)
        )
        {
            return null;
        }

        return DecodeImage(coverValue);
    }

    internal static byte[]? GetModelSpecThumbnail(IReadOnlyDictionary<string, string> metadataDict)
    {
        return metadataDict.TryGetValue("modelspec.thumbnail", out var thumbnail)
            ? DecodeImage(thumbnail)
            : null;
    }

    /// <summary>
    /// Whether a metadata key holds an embedded image.
    /// </summary>
    internal static bool IsImageKey(string key)
    {
        if (key == "modelspec.thumbnail")
        {
            return true;
        }

        return key.StartsWith("cover_", StringComparison.Ordinal)
            && key.Length > "cover_".Length
            && key.AsSpan("cover_".Length).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>
    /// Decodes a base64 image, with or without a <c>data:image/...;base64,</c> prefix.
    /// </summary>
    internal static byte[]? DecodeImage(string value)
    {
        var base64 = value.AsSpan().Trim();
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var separator = base64.IndexOf(',');
            if (separator < 0 || !base64[..separator].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            base64 = base64[(separator + 1)..];
        }

        if (base64.IsEmpty)
        {
            return null;
        }

        var buffer = new byte[base64.Length * 3 / 4];
        if (!Convert.TryFromBase64Chars(base64, buffer, out var bytesWritten) || bytesWritten == 0)
        {
            return null;
        }

        return bytesWritten == buffer.Length ? buffer : buffer[..bytesWritten];
    }

    internal static ConnectedModelInfo? GetConnectedModelInfo(
        IReadOnlyDictionary<string, string> metadataDict
    )
    {
        if (!metadataDict.TryGetValue("cm_info", out var cmInfoJson) || string.IsNullOrWhiteSpace(cmInfoJson))
        {
            return null;
        }

        ConnectedModelInfo? cmInfo;
        try
        {
            cmInfo = JsonSerializer.Deserialize(
                cmInfoJson,
                ConnectedModelInfoSerializerContext.Default.ConnectedModelInfo
            );
        }
        catch (JsonException)
        {
            return null;
        }

        if (cmInfo is null)
        {
            return null;
        }

        cmInfo.ModelName ??= string.Empty;
        cmInfo.ModelDescription ??= string.Empty;
        cmInfo.VersionName ??= string.Empty;
        cmInfo.Tags ??= [];
        cmInfo.Hashes ??= new CivitFileHashes();

        return cmInfo;
    }

    /// <summary>
    /// Gets the base model from <c>modelspec.architecture</c>, falling back to kohya-ss
    /// <c>ss_base_model_version</c>. Only architectures that map to one CivitAI base model are used.
    /// </summary>
    internal static string? GetBaseModel(IReadOnlyDictionary<string, string> metadataDict)
    {
        if (metadataDict.TryGetValue("modelspec.architecture", out var architecture))
        {
            var slash = architecture.IndexOf('/');
            var baseArchitecture = (slash < 0 ? architecture : architecture[..slash]).Trim();

            var baseModel = baseArchitecture.ToLowerInvariant() switch
            {
                "stable-diffusion-v1" => CivitBaseModelType.Sd15,
                "stable-diffusion-xl-v1-base" => CivitBaseModelType.Sdxl10,
                "stable-diffusion-v3-medium" => CivitBaseModelType.Sd3,
                "stable-diffusion-v3.5-medium" => CivitBaseModelType.Sd35Medium,
                "stable-diffusion-v3.5-large" => CivitBaseModelType.Sd35Large,
                "flux-1-dev" => CivitBaseModelType.Flux1D,
                "flux-1-schnell" => CivitBaseModelType.Flux1S,
                _ => (CivitBaseModelType?)null,
            };

            if (baseModel is not null)
            {
                return baseModel.Value.GetStringValue();
            }
        }

        if (metadataDict.TryGetValue("ss_base_model_version", out var kohyaBaseModel))
        {
            var normalized = kohyaBaseModel.Trim().ToLowerInvariant();
            if (normalized.StartsWith("sd_v1", StringComparison.Ordinal))
            {
                return CivitBaseModelType.Sd15.GetStringValue();
            }

            if (normalized.StartsWith("sdxl", StringComparison.Ordinal))
            {
                return CivitBaseModelType.Sdxl10.GetStringValue();
            }
        }

        return null;
    }

    internal static string[]? GetTrainedWords(IReadOnlyDictionary<string, string> metadataDict)
    {
        if (!metadataDict.TryGetValue("modelspec.trigger_phrase", out var triggerPhrase))
        {
            return null;
        }

        var words = triggerPhrase
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return words.Length > 0 ? words : null;
    }

    internal static string? GetOriginalSha256(IReadOnlyDictionary<string, string> metadataDict)
    {
        if (!metadataDict.TryGetValue("model_sha256_original", out var sha256))
        {
            return null;
        }

        sha256 = sha256.Trim();
        return sha256.Length == 64 && sha256.All(char.IsAsciiHexDigit) ? sha256.ToLowerInvariant() : null;
    }
}
