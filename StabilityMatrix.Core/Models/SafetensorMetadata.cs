using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace StabilityMatrix.Core.Models;

public readonly record struct SafetensorMetadata
{
    // public string? NetworkModule { get; init; }
    // public string? ModelSpecArchitecture { get; init; }

    public List<Tag>? TagFrequency { get; init; }

    public List<Metadata> OtherMetadata { get; init; }

    public static bool TryParse(string safetensorPath, [MaybeNullWhen(false)] out SafetensorMetadata metadata)
    {
        metadata = default;
        try
        {
            using var stream = new FileStream(safetensorPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryParse(stream, out metadata);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryParse(
        Stream safetensorStream,
        [MaybeNullWhen(false)] out SafetensorMetadata metadata
    )
    {
        metadata = default;
        try
        {
            // 8 bytes unsigned little-endian 64-bit integer
            // 1 byte start of JSON object '{'
            Span<byte> buffer = stackalloc byte[9];
            safetensorStream.ReadExactly(buffer);

            const ulong MAX_ALLOWED_JSON_LENGTH = 100 * 1024 * 1024; // 100 MB
            var jsonLength = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
            if (jsonLength > MAX_ALLOWED_JSON_LENGTH)
            {
                return false;
            }
            if (buffer[8] != '{')
            {
                return false;
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
                safetensorStream.ReadExactly(jsonBytes, 1, (int)(jsonLength - 1));

                // read the JSON with Utf8JsonReader, then only deserialize what we need
                // saves us from allocating a bunch of strings then throwing them away
                var reader = new Utf8JsonReader(jsonBytes.AsSpan(0, (int)jsonLength));

                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    // expecting a JSON object
                    return false;
                }

                while (reader.Read())
                {
                    // for each property in the object
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        // end of the object, no "__metadata__" found
                        return false;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        // expecting a property name
                        return false;
                    }

                    if (reader.ValueTextEquals("__metadata__"))
                    {
                        if (JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader) is { } dict)
                        {
                            if (dict.Count == 0)
                            {
                                // got empty dictionary
                                return false;
                            }

                            metadata = FromDictionary(dict);
                            return true;
                        }

                        // got null
                        return false;
                    }
                    else
                    {
                        // skip the property value
                        reader.Skip();
                    }
                }
                // should not reach here, json is malformed
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(jsonBytes);
            }
        }
        catch
        {
            return false;
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

    public readonly record struct Tag(string Name, int Frequency)
    {
        public static implicit operator Tag((string name, int frequency) value)
        {
            return new Tag(value.name, value.frequency);
        }
    }

    public readonly record struct Metadata(string Name, string Value)
    {
        public static implicit operator Metadata((string name, string value) value)
        {
            return new Metadata(value.name, value.value);
        }
    }
}
