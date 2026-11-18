using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace StabilityMatrix.Core.Models;

public record SafetensorMetadata
{
    // public string? NetworkModule { get; init; }
    // public string? ModelSpecArchitecture { get; init; }

    public List<Tag>? TagFrequency { get; init; }

    public required List<Metadata> OtherMetadata { get; init; }

    /// <summary>
    /// Tries to parse the metadata from a SafeTensor file.
    /// </summary>
    /// <param name="safetensorPath">Path to the SafeTensor file.</param>
    /// <returns>The parsed metadata. Can be <see langword="null"/> if the file does not contain metadata.</returns>
    public static async Task<SafetensorMetadata?> ParseAsync(string safetensorPath)
    {
        using var stream = new FileStream(safetensorPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ParseAsync(stream);
    }

    /// <summary>
    /// Tries to parse the metadata from a SafeTensor file.
    /// </summary>
    /// <param name="safetensorStream">Stream to the SafeTensor file.</param>
    /// <returns>The parsed metadata. Can be <see langword="null"/> if the file does not contain metadata.</returns>
    public static async Task<SafetensorMetadata?> ParseAsync(Stream safetensorStream)
    {
        var metadataDict = await ReadMetadataDictionaryAsync(safetensorStream).ConfigureAwait(false);
        return metadataDict is null ? null : FromDictionary(metadataDict);
    }

    /// <summary>
    /// Reads the cover image embedded in the metadata of a SafeTensor file, in the
    /// LoRA-Container format (<c>cover_count</c> and <c>cover_1</c> ... <c>cover_N</c> keys).
    /// </summary>
    /// <param name="safetensorPath">Path to the SafeTensor file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JPEG bytes of the cover, or <see langword="null"/> if the file has no embedded cover.</returns>
    public static async Task<byte[]?> ReadEmbeddedCoverAsync(
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
        var metadataDict = await ReadMetadataDictionaryAsync(stream, cancellationToken).ConfigureAwait(false);
        return metadataDict is null ? null : GetEmbeddedCover(metadataDict);
    }

    /// <summary>
    /// Gets an embedded cover image from LoRA-Container metadata.
    /// <c>cover_1</c> is the preview image, <c>cover_2</c> onwards are gallery images.
    /// </summary>
    /// <param name="metadataDict">The <c>__metadata__</c> entries of a SafeTensor file.</param>
    /// <param name="coverNumber">1-based number of the cover to read.</param>
    /// <returns>The JPEG bytes of the cover, or <see langword="null"/> if it is missing or invalid.</returns>
    internal static byte[]? GetEmbeddedCover(
        IReadOnlyDictionary<string, string> metadataDict,
        int coverNumber = 1
    )
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

        const string DataUriPrefix = "data:image/jpeg;base64,";
        var base64 = coverValue.AsSpan().Trim();
        if (base64.StartsWith(DataUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            base64 = base64[DataUriPrefix.Length..];
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

    /// <summary>
    /// Reads the raw <c>__metadata__</c> entries from a SafeTensor file.
    /// </summary>
    /// <returns>The metadata entries, or <see langword="null"/> if the file does not contain metadata.</returns>
    private static async Task<Dictionary<string, string>?> ReadMetadataDictionaryAsync(
        Stream safetensorStream,
        CancellationToken cancellationToken = default
    )
    {
        // 8 bytes unsigned little-endian 64-bit integer
        // 1 byte start of JSON object '{'
        Memory<byte> buffer = new byte[9];
        await safetensorStream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        var span = buffer.Span;

        const ulong MAX_ALLOWED_JSON_LENGTH = 100 * 1024 * 1024; // 100 MB
        var jsonLength = BinaryPrimitives.ReadUInt64LittleEndian(span);
        if (jsonLength > MAX_ALLOWED_JSON_LENGTH)
        {
            throw new InvalidDataException("JSON length exceeds the maximum allowed size.");
        }
        if (span[8] != '{')
        {
            throw new InvalidDataException("JSON does not start with '{'.");
        }

        // Unfornately Utf8JsonReader does not support reading from a stream directly.
        // Usually the size of the entire JSON object is less than 500KB,
        // using a pooled buffer should reduce the number of large allocations.
        var jsonBytes = ArrayPool<byte>.Shared.Rent((int)jsonLength);
        try
        {
            // Important: the length of the rented buffer can be larger than jsonLength
            // and there can be additional junk data at the end.

            // we already read {, so start from index 1
            jsonBytes[0] = (byte)'{';
            await safetensorStream
                .ReadExactlyAsync(jsonBytes.AsMemory(1, (int)(jsonLength - 1)), cancellationToken)
                .ConfigureAwait(false);

            // read the JSON with Utf8JsonReader, then only deserialize what we need
            // saves us from allocating a bunch of strings then throwing them away
            var reader = new Utf8JsonReader(jsonBytes.AsSpan(0, (int)jsonLength));

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                // expecting a JSON object
                throw new InvalidDataException("JSON does not start with '{'.");
            }

            while (reader.Read())
            {
                // for each property in the object
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    // end of the object, no "__metadata__" found
                    // return true to indicate that we successfully read the JSON
                    // but it does not contain metadata
                    return null;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    // expecting a property name
                    throw new InvalidDataException(
                        $"Invalid metadata JSON, expected property name but got {reader.TokenType}."
                    );
                }

                if (reader.ValueTextEquals("__metadata__"))
                {
                    if (JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader) is { } dict)
                    {
                        return dict;
                    }

                    // got null from Deserialize
                    throw new InvalidDataException("Failed to deserialize metadata.");
                }
                else
                {
                    // skip the property value
                    reader.Skip();
                }
            }
            // should not reach here, json is malformed
            throw new InvalidDataException("Invalid metadata JSON.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(jsonBytes);
        }
    }

    private static readonly HashSet<string> MetadataKeys =
    [
        // "ss_network_module",
        // "modelspec.architecture",
        "ss_tag_frequency",
    ];

    internal static SafetensorMetadata FromDictionary(Dictionary<string, string> metadataDict)
    {
        // equivalent to the following code, rewitten manually for performance
        // otherMetadata = metadataDict
        //     .Where(kv => !MetadataKeys.Contains(kv.Key))
        //     .Select(kv => new Metadata(kv.Key, kv.Value))
        //     .OrderBy(x => x.Name)
        //     .ToList();
        var otherMetadata = new List<Metadata>(metadataDict.Count);
        foreach (var kv in metadataDict)
        {
            if (MetadataKeys.Contains(kv.Key))
            {
                continue;
            }

            otherMetadata.Add(new Metadata(kv.Key, kv.Value));
        }
        otherMetadata.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));

        var metadata = new SafetensorMetadata
        {
            // NetworkModule = metadataDict.GetValueOrDefault("ss_network_module"),
            // ModelSpecArchitecture = metadataDict.GetValueOrDefault("modelspec.architecture"),
            OtherMetadata = otherMetadata
        };

        if (metadataDict.TryGetValue("ss_tag_frequency", out var tagFrequencyJson))
        {
            try
            {
                // ss_tag_frequency example:
                // { "some_name": {"tag1": 5, "tag2": 10}, "another_name": {"tag1": 3, "tag3": 1} }
                // we flatten the dictionary of dictionaries into a single dictionary

                var tagFrequencyDict = new Dictionary<string, int>();

                var doc = JsonDocument.Parse(tagFrequencyJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        var tags = property.Value;
                        if (tags.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        foreach (var tagProperty in tags.EnumerateObject())
                        {
                            var tagName = tagProperty.Name;

                            if (
                                string.IsNullOrEmpty(tagName)
                                || tagProperty.Value.ValueKind != JsonValueKind.Number
                            )
                            {
                                continue;
                            }

                            var count = tagProperty.Value.GetInt32();
                            if (!tagFrequencyDict.TryAdd(tagName, count))
                            {
                                // tag already exists, increment the count
                                tagFrequencyDict[tagName] += count;
                            }
                        }
                    }
                }

                // equivalent to the following code, rewitten manually for performance
                // tagFrequency = tagFrequencyDict
                //     .Select(kv => new Tag(kv.Key, kv.Value))
                //     .OrderByDescending(x => x.Frequency)
                //     .ToList();
                var tagFrequency = new List<Tag>(tagFrequencyDict.Count);
                foreach (var kv in tagFrequencyDict)
                {
                    tagFrequency.Add(new Tag(kv.Key, kv.Value));
                }
                tagFrequency.Sort((x, y) => y.Frequency.CompareTo(x.Frequency));

                metadata = metadata with { TagFrequency = tagFrequency };
            }
            catch (Exception)
            {
                // ignore
            }
        }

        return metadata;
    }

    public readonly record struct Tag(string Name, int Frequency);

    public readonly record struct Metadata(string Name, string Value);
}
