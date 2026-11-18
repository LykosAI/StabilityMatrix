using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class SafetensorMetadataTests
{
    [TestMethod]
    public async Task TestParseStreamAsync()
    {
        const string SOURCE_JSON = """
{
"anything":[1,2,3,4,"",{ "a": 1, "b": 2, "c": 3 }],
"__metadata__":{"ss_network_module":"some network module","modelspec.architecture":"some architecture",
    "ss_tag_frequency":"{\"aaa\":{\"tag1\":59,\"tag2\":2},\"bbb\":{\"tag1\":4,\"tag3\":1}}" },
"someotherdata":{ "a": 1, "b": 2, "c": 3 }
}
""";

        var stream = new MemoryStream();
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, (ulong)SOURCE_JSON.Length);
        stream.Write(buffer);
        stream.Write(Encoding.UTF8.GetBytes(SOURCE_JSON));
        stream.Position = 0;

        var metadata = await SafetensorMetadata.ParseAsync(stream);

        // Assert.AreEqual("some network module", metadata.NetworkModule);
        // Assert.AreEqual("some architecture", metadata.ModelSpecArchitecture);

        Assert.IsNotNull(metadata);
        Assert.IsNotNull(metadata.TagFrequency);
        CollectionAssert.AreEqual(
            new List<SafetensorMetadata.Tag> { new("tag1", 63), new("tag2", 2), new("tag3", 1) },
            metadata.TagFrequency
        );
    }

    private static readonly byte[] CoverJpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9];
    private static readonly byte[] GalleryJpegBytes = [0xFF, 0xD8, 0x01, 0x02, 0x03, 0xFF, 0xD9];

    [TestMethod]
    public async Task ReadEmbeddedCoverAsync_ReturnsFirstCover()
    {
        var path = WriteSafetensorFile(
            new Dictionary<string, string>
            {
                ["ss_network_module"] = "networks.lora",
                ["cover_format"] = "1",
                ["cover_count"] = "2",
                ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
                ["cover_2"] = Convert.ToBase64String(GalleryJpegBytes),
            }
        );

        try
        {
            var cover = await SafetensorMetadata.ReadEmbeddedCoverAsync(path);

            CollectionAssert.AreEqual(CoverJpegBytes, cover);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReadEmbeddedCoverAsync_ReturnsNull_WhenFileHasNoMetadata()
    {
        var path = WriteSafetensorFile(null);

        try
        {
            Assert.IsNull(await SafetensorMetadata.ReadEmbeddedCoverAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void GetEmbeddedCover_ReturnsGalleryCover()
    {
        var metadata = new Dictionary<string, string>
        {
            ["cover_count"] = "2",
            ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
            ["cover_2"] = Convert.ToBase64String(GalleryJpegBytes),
        };

        CollectionAssert.AreEqual(GalleryJpegBytes, SafetensorMetadata.GetEmbeddedCover(metadata, 2));
        Assert.IsNull(SafetensorMetadata.GetEmbeddedCover(metadata, 3));
    }

    [TestMethod]
    public void GetEmbeddedCover_AcceptsDataUriPrefix()
    {
        var metadata = new Dictionary<string, string>
        {
            ["cover_count"] = "1",
            ["cover_1"] = "data:image/jpeg;base64," + Convert.ToBase64String(CoverJpegBytes),
        };

        CollectionAssert.AreEqual(CoverJpegBytes, SafetensorMetadata.GetEmbeddedCover(metadata));
    }

    [TestMethod]
    public void GetEmbeddedCover_ReturnsNull_WhenCoverCountIsMissing()
    {
        var metadata = new Dictionary<string, string>
        {
            ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
        };

        Assert.IsNull(SafetensorMetadata.GetEmbeddedCover(metadata));
    }

    [TestMethod]
    public void GetEmbeddedCover_ReturnsNull_WhenCoverIsNotBase64()
    {
        var metadata = new Dictionary<string, string> { ["cover_count"] = "1", ["cover_1"] = "not base64!" };

        Assert.IsNull(SafetensorMetadata.GetEmbeddedCover(metadata));
    }

    private static string WriteSafetensorFile(Dictionary<string, string>? metadata)
    {
        var header = new Dictionary<string, object>
        {
            ["weight"] = new
            {
                dtype = "F32",
                shape = new[] { 1 },
                data_offsets = new[] { 0, 4 },
            },
        };
        if (metadata is not null)
        {
            header["__metadata__"] = metadata;
        }

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
        var path = Path.GetTempFileName();

        using var stream = File.Create(path);
        Span<byte> lengthBuffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(lengthBuffer, (ulong)headerBytes.Length);
        stream.Write(lengthBuffer);
        stream.Write(headerBytes);
        stream.Write(new byte[4]);

        return path;
    }
}
