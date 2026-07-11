using System.Buffers.Binary;
using System.Text.Json;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Helper;

public static class SafetensorClassifier
{
    private static readonly string[] UnetTensorPrefixes =
    [
        // SD 1.x/2.x, SDXL
        "model.diffusion_model.",
        "diffusion_model.",
        "unet.",
        // Flux / DiT (BFL format, also used nested under model.* by Hunyuan3D etc.)
        "double_blocks.",
        "single_blocks.",
        "img_in.",
        "txt_in.",
        "time_in.",
        "vector_in.",
        "guidance_in.",
        "final_layer.",
        "model.double_blocks.",
        "model.single_blocks.",
        "model.time_in.",
        "model.final_layer.",
        "model.latent_in.",
        "model.cond_in.",
        // Flux (diffusers format)
        "transformer_blocks.",
        "single_transformer_blocks.",
        "context_embedder.",
        "x_embedder.",
        "time_text_embed.",
        "norm_out.",
        "proj_out.",
        // Wan Video
        "blocks.",
        "head.",
        "patch_embedding.",
        "text_embedding.",
        "time_embedding.",
        "time_projection.",
        "img_emb.",
        // HiDream
        "double_stream_blocks.",
        "single_stream_blocks.",
        "caption_projection.",
        "t_embedder.",
        "p_embedder.",
        // Z-Image
        "layers.",
        "cap_embedder.",
        "context_refiner.",
        "noise_refiner.",
    ];

    private static readonly string[] VaeTensorPrefixes = ["first_stage_model.", "vae.", "autoencoder."];

    private static readonly string[] TextEncoderTensorPrefixes =
    [
        "cond_stage_model.",
        "conditioner.",
        "text_encoder.",
        "text_encoders.",
        "clip_l.",
        "clip_g.",
        "t5xxl.",
        "te1.",
        "te2.",
    ];

    public static async Task<SafetensorCheckpointKind> ClassifyAsync(FilePath safetensorPath)
    {
        await using var stream = new FileStream(
            safetensorPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );

        var headerLengthBytes = new byte[8];
        await stream.ReadExactlyAsync(headerLengthBytes).ConfigureAwait(false);
        var headerLength = BinaryPrimitives.ReadUInt64LittleEndian(headerLengthBytes);

        const ulong maxAllowedHeaderLength = 100 * 1024 * 1024;
        if (headerLength is 0 or > maxAllowedHeaderLength)
        {
            return SafetensorCheckpointKind.Unknown;
        }

        var headerBuffer = new byte[(int)headerLength];
        await stream.ReadExactlyAsync(headerBuffer).ConfigureAwait(false);

        var reader = new Utf8JsonReader(headerBuffer);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return SafetensorCheckpointKind.Unknown;
        }

        var hasUnetWeights = false;
        var hasVaeWeights = false;
        var hasTextEncoderWeights = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                return SafetensorCheckpointKind.Unknown;
            }

            var tensorKey = reader.GetString();
            if (
                !string.IsNullOrWhiteSpace(tensorKey)
                && !tensorKey.Equals("__metadata__", StringComparison.Ordinal)
            )
            {
                hasUnetWeights |= StartsWithAny(tensorKey, UnetTensorPrefixes);
                hasVaeWeights |= StartsWithAny(tensorKey, VaeTensorPrefixes);
                hasTextEncoderWeights |= StartsWithAny(tensorKey, TextEncoderTensorPrefixes);

                if (hasUnetWeights && (hasVaeWeights || hasTextEncoderWeights))
                {
                    return SafetensorCheckpointKind.AioOrMixed;
                }
            }

            if (!reader.Read())
            {
                break;
            }

            reader.Skip();
        }

        return hasUnetWeights && !hasVaeWeights && !hasTextEncoderWeights
            ? SafetensorCheckpointKind.UnetOnly
            : SafetensorCheckpointKind.Unknown;
    }

    private static bool StartsWithAny(string value, IEnumerable<string> prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

public enum SafetensorCheckpointKind
{
    Unknown,
    UnetOnly,
    AioOrMixed,
}
