using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models.GenerationData;

public enum MetadataContainer
{
    Unknown,
    Png,
    Jpeg,
    Webp,
    Mp4,
    Matroska,
}

/// <summary>
/// Text metadata of an image or video, keyed by the name of the PNG chunk, EXIF tag,
/// XMP property or video tag it was read from. Only the metadata is read, never the image or media data.
/// </summary>
public partial class MetadataEntries
{
    private const int MaxTextLength = 64 * 1024 * 1024;
    private const int MaxBoxLength = 64 * 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const string ExifHeader = "Exif\0\0";
    private const string XmpHeader = "http://ns.adobe.com/xap/1.0/\0";
    private const string Mp4XmpUuid = "be7acfcb97a942e89c71999491e3afac";
    private const string Mp4C2paUuid = "d8fec3d61b0e483c92975828877ec481";

    /// <summary>
    /// PNG chunk names that tools pack into one JSON object, or label a value with,
    /// when they write JPEG or WEBP files (NovelAI, ComfyUI save nodes).
    /// </summary>
    private static readonly string[] PackedKeys =
    [
        "prompt",
        "workflow",
        "parameters",
        "Comment",
        "Description",
        "Software",
        "Source",
        "Title",
        "Generation time",
        "generation_data",
        "invokeai_metadata",
        "smproj",
        "parameters-json",
        "Copyright",
        "extra",
    ];

    private static readonly Dictionary<int, string> ExifTextTags = new()
    {
        [0x010e] = "ImageDescription",
        [0x010f] = "Make",
        [0x0110] = "Model",
        [0x0131] = "Software",
        [0x013b] = "Artist",
        [0x8298] = "Copyright",
        [0x927c] = "MakerNote",
        [0x9286] = "UserComment",
    };

    private static readonly Dictionary<string, string> IlstNames = new()
    {
        ["©cmt"] = "comment",
        ["desc"] = "description",
        ["ldes"] = "synopsis",
        ["©nam"] = "title",
        ["©too"] = "encoder",
        ["©swr"] = "encoder",
        ["©ART"] = "artist",
        ["©day"] = "date",
        ["©gen"] = "genre",
        ["©inf"] = "information",
        ["cprt"] = "copyright",
    };

    public MetadataContainer Container { get; private init; }

    public Dictionary<string, string> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The XMP packet, if any.
    /// </summary>
    public string? Xmp { get; private set; }

    /// <summary>
    /// The raw C2PA manifest store (JUMBF), if any.
    /// </summary>
    public byte[]? C2pa { get; private set; }

    public string? this[string key] => Entries.GetValueOrDefault(key);

    public bool IsEmpty => Entries.Count == 0 && Xmp is null && C2pa is null;

    private void Add(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Entries.TryAdd(key, value);
        }
    }

    public static MetadataEntries FromDictionary(
        IEnumerable<KeyValuePair<string, string>> entries,
        MetadataContainer container = MetadataContainer.Png
    )
    {
        var result = new MetadataEntries { Container = container };
        foreach (var (key, value) in entries)
        {
            result.Add(key, value);
        }
        return result;
    }

    public static MetadataEntries Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Read(stream);
    }

    public static MetadataEntries Read(Stream stream)
    {
        Span<byte> head = stackalloc byte[16];
        var headLength = stream.ReadAtLeast(head, head.Length, false);
        head = head[..headLength];
        stream.Position = 0;

        if (head.StartsWith(PngSignature))
            return ReadPng(stream);
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return ReadJpeg(stream);
        if (head.Length >= 12 && Ascii(head[..4]) == "RIFF" && Ascii(head[8..12]) == "WEBP")
            return ReadWebp(stream);
        if (head.Length >= 12 && Ascii(head[4..8]) == "ftyp")
            return ReadMp4(stream);
        if (head.Length >= 4 && head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3)
            return ReadMatroska(stream);

        return new MetadataEntries();
    }

    #region PNG

    private static MetadataEntries ReadPng(Stream stream)
    {
        var result = new MetadataEntries { Container = MetadataContainer.Png };
        stream.Position = 8;
        Span<byte> header = stackalloc byte[8];

        while (stream.ReadAtLeast(header, 8, false) == 8)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = Ascii(header[4..]);
            if (length > int.MaxValue || stream.Position + length > stream.Length)
                break;

            if (type is "tEXt" or "zTXt" or "iTXt" or "eXIf" or "caBX" && length <= MaxTextLength)
            {
                var data = ReadExactly(stream, (int)length);
                switch (type)
                {
                    case "eXIf":
                        // CivitAI writes its metadata to an eXIf chunk
                        foreach (var (key, value) in ReadExifEntries(data))
                            result.Add(key, value);
                        break;
                    case "caBX":
                        result.C2pa ??= data;
                        break;
                    default:
                        if (ReadPngTextChunk(type, data) is var (keyword, text))
                        {
                            result.Add(keyword, text);
                            if (keyword == "XML:com.adobe.xmp")
                                result.AddXmp(text);
                        }
                        break;
                }
            }
            else if (type == "IEND")
            {
                break;
            }
            else
            {
                stream.Seek(length, SeekOrigin.Current);
            }

            // crc
            stream.Seek(4, SeekOrigin.Current);
        }

        return result;
    }

    private static (string Keyword, string Text)? ReadPngTextChunk(string type, byte[] data)
    {
        var keyEnd = Array.IndexOf(data, (byte)0);
        if (keyEnd <= 0)
            return null;
        var keyword = Encoding.Latin1.GetString(data, 0, keyEnd);

        switch (type)
        {
            case "tEXt":
                return (keyword, DecodeText(data.AsSpan(keyEnd + 1)));
            case "zTXt":
            {
                if (keyEnd + 1 >= data.Length || data[keyEnd + 1] != 0)
                    return null;
                var inflated = Inflate(data.AsSpan(keyEnd + 2));
                return inflated is null ? null : (keyword, DecodeText(inflated));
            }
            case "iTXt":
            {
                if (keyEnd + 3 > data.Length)
                    return null;
                var compressed = data[keyEnd + 1] != 0;
                var langEnd = Array.IndexOf(data, (byte)0, keyEnd + 3);
                if (langEnd < 0)
                    return null;
                var translatedEnd = Array.IndexOf(data, (byte)0, langEnd + 1);
                if (translatedEnd < 0)
                    return null;
                var value = data.AsSpan(translatedEnd + 1);
                if (compressed)
                {
                    var inflated = Inflate(value);
                    if (inflated is null)
                        return null;
                    return (keyword, Encoding.UTF8.GetString(inflated));
                }
                return (keyword, Encoding.UTF8.GetString(value));
            }
            default:
                return null;
        }
    }

    private static byte[]? Inflate(ReadOnlySpan<byte> data)
    {
        try
        {
            using var input = new MemoryStream(data.ToArray());
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    #endregion

    #region JPEG / WEBP

    private static MetadataEntries ReadJpeg(Stream stream)
    {
        var result = new MetadataEntries { Container = MetadataContainer.Jpeg };
        var comments = new List<string>();
        using var c2pa = new MemoryStream();
        byte[]? exif = null;

        stream.Position = 2;
        Span<byte> marker = stackalloc byte[4];
        while (stream.ReadAtLeast(marker[..2], 2, false) == 2)
        {
            if (marker[0] != 0xFF)
                break;
            var type = marker[1];
            if (type == 0xFF)
            {
                stream.Seek(-1, SeekOrigin.Current);
                continue;
            }
            // start of scan or end of image: no metadata after this
            if (type is 0xDA or 0xD9)
                break;
            if (type == 0x01 || type is >= 0xD0 and <= 0xD7)
                continue;

            if (stream.ReadAtLeast(marker[2..], 2, false) != 2)
                break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(marker[2..]) - 2;
            if (length < 0 || stream.Position + length > stream.Length)
                break;

            if (type is 0xE1 or 0xFE or 0xEB)
            {
                var segment = ReadExactly(stream, length);
                if (type == 0xE1)
                {
                    if (exif is null && StartsWithAscii(segment, ExifHeader))
                        exif = segment;
                    else if (result.Xmp is null && StartsWithAscii(segment, XmpHeader))
                        result.Xmp = Encoding.UTF8.GetString(
                            segment,
                            XmpHeader.Length,
                            segment.Length - XmpHeader.Length
                        );
                }
                else if (type == 0xFE)
                {
                    var text = DecodeText(segment).TrimEnd('\0');
                    if (text.Length > 0)
                        comments.Add(text);
                }
                else if (segment.Length > 8 && segment[0] == (byte)'J' && segment[1] == (byte)'P')
                {
                    // APP11 JUMBF: common header, then the box data (repeated box header after the first segment)
                    var sequence = BinaryPrimitives.ReadUInt32BigEndian(segment.AsSpan(4));
                    c2pa.Write(segment, sequence <= 1 ? 8 : 16, segment.Length - (sequence <= 1 ? 8 : 16));
                }
            }
            else
            {
                stream.Seek(length, SeekOrigin.Current);
            }
        }

        if (exif is not null)
        {
            foreach (var (key, value) in ReadExifEntries(exif))
                result.Add(key, value);
        }
        if (comments.Count > 0)
            result.Add("Comment", string.Join('\n', comments));
        if (c2pa.Length > 0)
            result.C2pa = c2pa.ToArray();

        result.FinishExifImage();
        return result;
    }

    private static MetadataEntries ReadWebp(Stream stream)
    {
        var result = new MetadataEntries { Container = MetadataContainer.Webp };
        stream.Position = 12;
        Span<byte> header = stackalloc byte[8];
        byte[]? exif = null;

        while (stream.ReadAtLeast(header, 8, false) == 8)
        {
            var type = Ascii(header[..4]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            if (size > int.MaxValue || stream.Position + size > stream.Length)
                break;

            if (type is "EXIF" or "XMP " or "C2PA" && size <= MaxTextLength)
            {
                var data = ReadExactly(stream, (int)size);
                switch (type)
                {
                    case "EXIF":
                        exif ??= data;
                        break;
                    case "XMP ":
                        result.Xmp ??= Encoding.UTF8.GetString(data);
                        break;
                    default:
                        result.C2pa ??= data;
                        break;
                }
                stream.Seek(size % 2, SeekOrigin.Current);
            }
            else
            {
                stream.Seek(size + size % 2, SeekOrigin.Current);
            }
        }

        if (exif is not null)
        {
            foreach (var (key, value) in ReadExifEntries(exif))
                result.Add(key, value);
        }

        result.FinishExifImage();
        return result;
    }

    private void FinishExifImage()
    {
        if (Xmp is not null)
        {
            Add("XMP", Xmp);
            AddXmp(Xmp);
        }
        UnpackJsonEntries(this["UserComment"]);
        UnpackJsonEntries(this["Comment"]);
    }

    /// <summary>
    /// Add the values of a JSON object holding PNG chunk names as entries of their own
    /// </summary>
    private void UnpackJsonEntries(string? text)
    {
        if (text is null || !text.TrimStart().StartsWith('{'))
            return;
        if (GenerationDataJson.ParseObject(text) is not { } json)
            return;

        foreach (var key in PackedKeys)
        {
            if (Entries.ContainsKey(key) || json[key] is not { } value)
                continue;
            Add(key, GenerationDataJson.AsText(value));
        }
    }

    /// <summary>
    /// Read the text tags of an EXIF block (TIFF structure, with or without the Exif header)
    /// </summary>
    public static Dictionary<string, string> ReadExifEntries(byte[] exif)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var tiff = exif.AsSpan();
        if (StartsWithAscii(exif, ExifHeader))
            tiff = tiff[6..];
        if (tiff.Length < 8)
            return entries;

        var byteOrder = Ascii(tiff[..2]);
        if (byteOrder is not ("II" or "MM"))
            return entries;
        var littleEndian = byteOrder == "II";
        var data = tiff.ToArray();

        var visited = new HashSet<uint>();
        ReadIfd(U32(data, 4, littleEndian));
        return entries;

        void ReadIfd(uint ifdOffset)
        {
            if (!visited.Add(ifdOffset) || ifdOffset + 2 > data.Length)
                return;

            var count = U16(data, (int)ifdOffset, littleEndian);
            for (var i = 0; i < count; i++)
            {
                var entry = (int)ifdOffset + 2 + i * 12;
                if (entry + 12 > data.Length)
                    return;
                var tag = U16(data, entry, littleEndian);
                var type = U16(data, entry + 2, littleEndian);
                var size = (long)TypeSize(type) * U32(data, entry + 4, littleEndian);

                if (tag == 0x8769)
                {
                    ReadIfd(U32(data, entry + 8, littleEndian));
                    continue;
                }

                if (!ExifTextTags.TryGetValue(tag, out var name) || size > data.Length)
                    continue;
                long valueOffset = size <= 4 ? entry + 8 : U32(data, entry + 8, littleEndian);
                if (valueOffset + size > data.Length)
                    continue;
                var value = data.AsSpan((int)valueOffset, (int)size);

                string text;
                if (name == "UserComment")
                    text = DecodeUserComment(value, littleEndian);
                else if (type is 1 or 2 or 7)
                    text = DecodeText(value).TrimEnd('\0');
                else
                    continue;
                if (text.Length == 0)
                    continue;

                // ComfyUI save nodes label values with the PNG chunk they stand in for: "prompt:{...}", "Workflow: {...}"
                var label = ChunkLabelRegex().Match(text);
                if (
                    label.Success
                    && name != "UserComment"
                    && PackedKeys.Contains(label.Groups[1].Value.ToLowerInvariant())
                )
                {
                    entries.TryAdd(label.Groups[1].Value.ToLowerInvariant(), text[label.Length..]);
                }
                else
                {
                    entries.TryAdd(name, text);
                }
            }
        }
    }

    private static int TypeSize(int type) =>
        type switch
        {
            3 or 8 => 2,
            4 or 9 or 11 => 4,
            5 or 10 or 12 => 8,
            _ => 1,
        };

    /// <summary>
    /// Decode an EXIF UserComment value: an 8 byte character code followed by the text
    /// </summary>
    public static string DecodeUserComment(ReadOnlySpan<byte> value, bool littleEndianHint = false)
    {
        var code = value.Length >= 8 ? Encoding.Latin1.GetString(value[..8]) : string.Empty;
        var body = value.Length >= 8 ? value[8..] : value;
        if (code == "UNICODE\0")
            return DecodeUtf16Guess(body, littleEndianHint);
        if (code is "ASCII\0\0\0" or "\0\0\0\0\0\0\0\0")
            return DecodeText(body).TrimEnd('\0');
        // no character code: some ComfyUI save nodes write plain UTF-8
        return DecodeText(value).TrimEnd('\0');
    }

    /// <summary>
    /// Tools write UTF-16 in either byte order regardless of the TIFF header,
    /// generation data is mostly ASCII so the order that yields far more ASCII is the right one.
    /// </summary>
    private static string DecodeUtf16Guess(ReadOnlySpan<byte> data, bool littleEndianHint)
    {
        data = data[..(data.Length - data.Length % 2)];
        var preferred = (littleEndianHint ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(data);
        var other = (littleEndianHint ? Encoding.BigEndianUnicode : Encoding.Unicode).GetString(data);
        var preferredAscii = CountAscii(preferred);
        var otherAscii = CountAscii(other);
        var text = otherAscii > preferredAscii * 2 && otherAscii * 2 > other.Length ? other : preferred;
        return text.TrimStart('﻿').TrimEnd('\0');
    }

    private static int CountAscii(string text) =>
        text.Count(c => c is >= ' ' and < '\x7f' or '\n' or '\r' or '\t');

    #endregion

    #region XMP

    private void AddXmp(string xmp)
    {
        Xmp ??= xmp;
        Add("Description", GetXmpProperty(xmp, "dc:description"));
        Add("UserComment", GetXmpProperty(xmp, "exif:UserComment"));
        Add("CreatorTool", GetXmpProperty(xmp, "xmp:CreatorTool"));
        Add("DigitalSourceType", GetXmpProperty(xmp, "*:DigitalSourceType"));
        Add("Publisher", GetXmpProperty(xmp, "dc:publisher"));
    }

    /// <summary>
    /// Read a simple XMP property, written as an attribute or as an element
    /// (for language alternatives and lists, the first item).
    /// </summary>
    public static string? GetXmpProperty(string xmp, string name)
    {
        var escaped = name.StartsWith("*:") ? @"[\w.-]+:" + Regex.Escape(name[2..]) : Regex.Escape(name);

        var attribute = Regex.Match(xmp, $@"\s{escaped}\s*=\s*""([^""]*)""");
        if (!attribute.Success)
            attribute = Regex.Match(xmp, $@"\s{escaped}\s*=\s*'([^']*)'");
        if (attribute.Success)
            return DecodeXmlText(attribute.Groups[1].Value);

        var element = Regex.Match(xmp, $@"<({escaped})(?:\s[^>]*)?>([\s\S]*?)</\1>");
        if (!element.Success)
            return null;
        var item = Regex.Match(element.Groups[2].Value, @"<rdf:li(?:\s[^>]*)?>([\s\S]*?)</rdf:li>");
        return DecodeXmlText((item.Success ? item.Groups[1].Value : element.Groups[2].Value).Trim());
    }

    private static string DecodeXmlText(string text) => System.Net.WebUtility.HtmlDecode(text);

    #endregion

    #region MP4 / MOV

    private static MetadataEntries ReadMp4(Stream stream)
    {
        var result = new MetadataEntries { Container = MetadataContainer.Mp4 };
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (type, start, length) in ReadBoxHeaders(stream, 0, stream.Length))
        {
            if (type == "moov")
            {
                foreach (var (childType, childStart, childLength) in ReadBoxHeaders(stream, start, length))
                {
                    if (childType is not ("meta" or "udta") || childLength > MaxBoxLength)
                        continue;
                    stream.Position = childStart;
                    var data = ReadExactly(stream, (int)childLength);
                    if (childType == "meta")
                        ReadMp4Meta(data, raw);
                    else
                        ReadMp4Udta(data, raw, result);
                }
            }
            else if (type == "uuid" && length >= 16 && length <= MaxBoxLength)
            {
                stream.Position = start;
                var data = ReadExactly(stream, (int)length);
                var uuid = Convert.ToHexStringLower(data, 0, 16);
                if (uuid == Mp4XmpUuid)
                {
                    result.Xmp ??= Encoding.UTF8.GetString(data, 16, data.Length - 16);
                }
                else if (uuid == Mp4C2paUuid && data.Length > 20)
                {
                    var body = data.AsSpan(20);
                    var end = body.IndexOf((byte)0);
                    if (
                        end > 0
                        && Encoding.Latin1.GetString(body[..end]) == "manifest"
                        && end + 9 <= body.Length
                    )
                        result.C2pa ??= body[(end + 9)..].ToArray();
                }
            }
        }

        result.FinishVideo(raw);
        return result;
    }

    private static IEnumerable<(string Type, long Start, long Length)> ReadBoxHeaders(
        Stream stream,
        long offset,
        long length
    )
    {
        var end = Math.Min(offset + length, stream.Length);
        var header = new byte[16];
        while (offset + 8 <= end)
        {
            stream.Position = offset;
            if (stream.ReadAtLeast(header.AsSpan(0, 8), 8, false) != 8)
                yield break;
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = Encoding.Latin1.GetString(header, 4, 4);
            var headerSize = 8;
            if (size == 1)
            {
                if (stream.ReadAtLeast(header.AsSpan(8, 8), 8, false) != 8)
                    yield break;
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
                headerSize = 16;
            }
            else if (size == 0)
            {
                size = end - offset;
            }
            if (size < headerSize)
                yield break;

            yield return (type, offset + headerSize, Math.Min(size, end - offset) - headerSize);
            offset += size;
        }
    }

    private static List<(string Type, byte[] Data)> ReadBoxes(byte[] data)
    {
        using var stream = new MemoryStream(data, false);
        var boxes = new List<(string, byte[])>();
        foreach (var (type, start, length) in ReadBoxHeaders(stream, 0, data.Length))
        {
            boxes.Add((type, data.AsSpan((int)start, (int)length).ToArray()));
        }
        return boxes;
    }

    private static void ReadMp4Meta(byte[] meta, Dictionary<string, string> raw)
    {
        // a full box in MP4 files (version and flags first) but not always in QuickTime files
        var boxes = ReadBoxes(meta);
        if (!boxes.Any(b => b.Type == "hdlr") && meta.Length > 4)
            boxes = ReadBoxes(meta[4..]);

        var keys = boxes.FirstOrDefault(b => b.Type == "keys").Data;
        var ilst = boxes.FirstOrDefault(b => b.Type == "ilst").Data;
        if (ilst is null)
            return;

        var keyNames = new List<string>();
        if (keys is not null)
        {
            var offset = 8;
            while (offset + 8 <= keys.Length)
            {
                var size = (int)BinaryPrimitives.ReadUInt32BigEndian(keys.AsSpan(offset));
                if (size < 8 || offset + size > keys.Length)
                    break;
                keyNames.Add(Encoding.UTF8.GetString(keys, offset + 8, size - 8));
                offset += size;
            }
        }

        foreach (var (type, item) in ReadBoxes(ilst))
        {
            var dataBox = ReadBoxes(item).FirstOrDefault(b => b.Type == "data").Data;
            if (dataBox is null || dataBox.Length < 8)
                continue;
            var dataType = BinaryPrimitives.ReadUInt32BigEndian(dataBox) & 0xFFFFFF;
            if (dataType is not (1 or 2))
                continue;
            var value =
                dataType == 1
                    ? Encoding.UTF8.GetString(dataBox, 8, dataBox.Length - 8)
                    : Encoding.BigEndianUnicode.GetString(dataBox, 8, dataBox.Length - 8);

            string name;
            if (type[0] == '\0')
            {
                var index = type.Aggregate(0, (total, c) => total * 256 + c);
                if (index < 1 || index > keyNames.Count)
                    continue;
                name = keyNames[index - 1].Replace("com.apple.quicktime.", "");
            }
            else
            {
                name = IlstNames.GetValueOrDefault(type, type);
            }
            raw.TryAdd(name, value);
        }
    }

    private static void ReadMp4Udta(byte[] udta, Dictionary<string, string> raw, MetadataEntries result)
    {
        foreach (var (type, data) in ReadBoxes(udta))
        {
            if (type == "meta")
            {
                ReadMp4Meta(data, raw);
            }
            else if (type == "XMP_")
            {
                result.Xmp ??= Encoding.UTF8.GetString(data);
            }
            else if (type[0] == '©' && data.Length >= 4)
            {
                // QuickTime user data text: 16 bit length, language code, text
                var length = (data[0] << 8) | data[1];
                if (length > 0 && 4 + length <= data.Length)
                    raw.TryAdd(
                        IlstNames.GetValueOrDefault(type, type),
                        Encoding.UTF8.GetString(data, 4, length)
                    );
            }
        }
    }

    #endregion

    #region Matroska / WebM

    private const uint EbmlSegment = 0x18538067;
    private const uint EbmlInfo = 0x1549a966;
    private const uint EbmlTitle = 0x7ba9;
    private const uint EbmlCluster = 0x1f43b675;
    private const uint EbmlTags = 0x1254c367;
    private const uint EbmlTag = 0x7373;
    private const uint EbmlTargets = 0x63c0;
    private const uint EbmlSimpleTag = 0x67c8;
    private const uint EbmlTagName = 0x45a3;
    private const uint EbmlTagString = 0x4487;
    private static readonly HashSet<uint> EbmlPartTargets = [0x63c5, 0x63c9, 0x63c4, 0x63c6];

    private static MetadataEntries ReadMatroska(Stream stream)
    {
        var result = new MetadataEntries { Container = MetadataContainer.Matroska };
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);

        var segment = ReadEbmlHeaders(stream, 0, stream.Length).FirstOrDefault(e => e.Id == EbmlSegment);
        if (segment.Id == EbmlSegment)
        {
            foreach (
                var (id, start, length, unknownSize) in ReadEbmlHeaders(stream, segment.Start, segment.Length)
            )
            {
                // clusters of unknown size can't be skipped, and tags come after them
                if (unknownSize && id == EbmlCluster)
                    break;
                if (id is not (EbmlTags or EbmlInfo) || length > MaxBoxLength)
                    continue;

                stream.Position = start;
                var data = ReadExactly(stream, (int)length);
                if (id == EbmlInfo)
                {
                    var title = ReadEbml(data).FirstOrDefault(e => e.Id == EbmlTitle);
                    if (title.Data is not null)
                        raw.TryAdd("TITLE", Encoding.UTF8.GetString(title.Data));
                    continue;
                }

                foreach (var tag in ReadEbml(data).Where(e => e.Id == EbmlTag))
                {
                    var targets = ReadEbml(tag.Data).FirstOrDefault(e => e.Id == EbmlTargets);
                    if (
                        targets.Data is not null
                        && ReadEbml(targets.Data).Any(t => EbmlPartTargets.Contains(t.Id))
                    )
                        continue;
                    ReadSimpleTags(tag.Data, raw);
                }
            }
        }

        result.FinishVideo(raw);
        return result;
    }

    private static void ReadSimpleTags(byte[] data, Dictionary<string, string> raw)
    {
        foreach (var simpleTag in ReadEbml(data).Where(e => e.Id == EbmlSimpleTag))
        {
            var children = ReadEbml(simpleTag.Data);
            var name = children.FirstOrDefault(e => e.Id == EbmlTagName).Data;
            var value = children.FirstOrDefault(e => e.Id == EbmlTagString).Data;
            if (name is not null && value is not null)
                raw.TryAdd(Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(value));
            ReadSimpleTags(simpleTag.Data, raw);
        }
    }

    private static (ulong Value, int Length, bool Unknown)? ReadVint(ReadOnlySpan<byte> data, bool keepMarker)
    {
        if (data.Length == 0 || data[0] == 0)
            return null;
        var length = System.Numerics.BitOperations.LeadingZeroCount((uint)data[0]) - 23;
        if (length > data.Length || length > 8)
            return null;
        ulong value = keepMarker ? data[0] : (ulong)(data[0] & (0xFF >> length));
        var allOnes = value == (ulong)(0xFF >> length);
        for (var i = 1; i < length; i++)
        {
            value = value * 256 + data[i];
            if (data[i] != 0xFF)
                allOnes = false;
        }
        return (value, length, !keepMarker && allOnes);
    }

    private static IEnumerable<(uint Id, long Start, long Length, bool UnknownSize)> ReadEbmlHeaders(
        Stream stream,
        long offset,
        long length
    )
    {
        var end = Math.Min(offset + length, stream.Length);
        var header = new byte[16];
        while (offset < end)
        {
            stream.Position = offset;
            var read = stream.ReadAtLeast(header, header.Length, false);
            var span = header.AsSpan(0, read);
            if (ReadVint(span, true) is not { } id || ReadVint(span[id.Length..], false) is not { } size)
                yield break;
            var start = offset + id.Length + size.Length;
            var elementEnd = size.Unknown ? end : Math.Min(start + (long)size.Value, end);
            yield return ((uint)id.Value, start, elementEnd - start, size.Unknown);
            offset = elementEnd;
        }
    }

    private static List<(uint Id, byte[] Data)> ReadEbml(byte[] data)
    {
        var elements = new List<(uint, byte[])>();
        var offset = 0;
        while (offset < data.Length)
        {
            var span = data.AsSpan(offset);
            if (ReadVint(span, true) is not { } id || ReadVint(span[id.Length..], false) is not { } size)
                break;
            var start = offset + id.Length + size.Length;
            var end = size.Unknown
                ? data.Length
                : (int)Math.Min((ulong)start + size.Value, (ulong)data.Length);
            if (start > end)
                break;
            elements.Add(((uint)id.Value, data[start..end]));
            offset = end;
        }
        return elements;
    }

    #endregion

    /// <summary>
    /// Video tags under lower-case names. AUTOMATIC1111 style parameters, which Forge writes
    /// to the description, are also stored under "parameters".
    /// </summary>
    private void FinishVideo(Dictionary<string, string> raw)
    {
        foreach (var (key, value) in raw)
            Add(key.ToLowerInvariant(), value);

        var parameters = new[] { this["parameters"], this["description"], this["comment"] }.FirstOrDefault(
            v => v is not null && A1111StepsRegex().IsMatch(v)
        );
        if (parameters is not null)
            Entries["parameters"] = parameters;

        if (Xmp is not null)
            AddXmp(Xmp);
    }

    #region Helpers

    private static byte[] ReadExactly(Stream stream, int length)
    {
        var buffer = new byte[length];
        var read = stream.ReadAtLeast(buffer, length, false);
        return read == length ? buffer : buffer[..read];
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

    private static bool StartsWithAscii(byte[] data, string prefix) =>
        data.Length >= prefix.Length && Encoding.Latin1.GetString(data, 0, prefix.Length) == prefix;

    private static ushort U16(byte[] data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset))
            : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));

    private static uint U32(byte[] data, int offset, bool littleEndian) =>
        offset + 4 > data.Length ? uint.MaxValue
        : littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset))
        : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));

    /// <summary>
    /// Text that is not valid UTF-8 is read as Latin-1, as the PNG specification asks for.
    /// </summary>
    private static string DecodeText(ReadOnlySpan<byte> data)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(data);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(data);
        }
    }

    [GeneratedRegex(@"^([A-Za-z]+):(?:\s|(?=[{\[]))")]
    private static partial Regex ChunkLabelRegex();

    [GeneratedRegex(@"(?:^|\n)Steps: \d")]
    private static partial Regex A1111StepsRegex();

    #endregion
}
