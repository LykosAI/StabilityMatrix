using System.Buffers.Binary;
using System.Text.Json;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class SafetensorEmbeddedInfoTests
{
    private static readonly byte[] CoverJpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0xFF, 0xD9];
    private static readonly byte[] GalleryJpegBytes = [0xFF, 0xD8, 0x01, 0x02, 0x03, 0xFF, 0xD9];
    private const string OriginalSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task ReadAsync_ReadsLoraContainerMetadata()
    {
        var cmInfo = """
            {"ModelId":123,"ModelName":"My LoRA","BaseModel":"Illustrious","TrainedWords":["mychar"],
             "Hashes":{"SHA256":"ABC"},"ThumbnailImageUrl":null}
            """;
        var path = WriteSafetensorFile(
            new Dictionary<string, string>
            {
                ["ss_base_model_version"] = "sdxl_base_v1-0",
                ["cover_format"] = "1",
                ["cover_count"] = "2",
                ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
                ["cover_2"] = Convert.ToBase64String(GalleryJpegBytes),
                ["model_sha256_original"] = OriginalSha256.ToUpperInvariant(),
                ["cm_info"] = cmInfo,
            }
        );

        try
        {
            var info = await SafetensorEmbeddedInfo.ReadAsync(path);

            Assert.IsNotNull(info);
            CollectionAssert.AreEqual(CoverJpegBytes, info.Cover);
            Assert.AreEqual(OriginalSha256, info.OriginalSha256);
            Assert.AreEqual("SDXL 1.0", info.BaseModel);
            Assert.IsNotNull(info.ConnectedModelInfo);
            Assert.AreEqual(123, info.ConnectedModelInfo.ModelId);
            Assert.AreEqual("My LoRA", info.ConnectedModelInfo.ModelName);
            Assert.AreEqual("Illustrious", info.ConnectedModelInfo.BaseModel);
            CollectionAssert.AreEqual(new[] { "mychar" }, info.ConnectedModelInfo.TrainedWords);
            Assert.AreEqual(string.Empty, info.ConnectedModelInfo.VersionName);
            Assert.AreEqual(0, info.ConnectedModelInfo.Tags.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReadAsync_ReturnsNull_WhenFileHasNoMetadata()
    {
        var path = WriteSafetensorFile(null);

        try
        {
            Assert.IsNull(await SafetensorEmbeddedInfo.ReadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void FromMetadata_IsEmpty_WhenNothingUsefulIsEmbedded()
    {
        var info = SafetensorEmbeddedInfo.FromMetadata(
            new Dictionary<string, string> { ["ss_network_module"] = "networks.lora" }
        );

        Assert.IsTrue(info.IsEmpty);
    }

    [TestMethod]
    public void GetCover_ReturnsGalleryCover()
    {
        var metadata = new Dictionary<string, string>
        {
            ["cover_count"] = "2",
            ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
            ["cover_2"] = Convert.ToBase64String(GalleryJpegBytes),
        };

        CollectionAssert.AreEqual(GalleryJpegBytes, SafetensorEmbeddedInfo.GetCover(metadata, 2));
        Assert.IsNull(SafetensorEmbeddedInfo.GetCover(metadata, 3));
    }

    [TestMethod]
    public void GetCover_AcceptsDataUriPrefix()
    {
        var metadata = new Dictionary<string, string>
        {
            ["cover_count"] = "1",
            ["cover_1"] = "data:image/jpeg;base64," + Convert.ToBase64String(CoverJpegBytes),
        };

        CollectionAssert.AreEqual(CoverJpegBytes, SafetensorEmbeddedInfo.GetCover(metadata));
    }

    [TestMethod]
    public void GetCover_ReturnsNull_WhenCoverCountIsMissing()
    {
        var metadata = new Dictionary<string, string>
        {
            ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
        };

        Assert.IsNull(SafetensorEmbeddedInfo.GetCover(metadata));
    }

    [TestMethod]
    public void GetCover_ReturnsNull_WhenCoverIsNotBase64()
    {
        var metadata = new Dictionary<string, string> { ["cover_count"] = "1", ["cover_1"] = "not base64!" };

        Assert.IsNull(SafetensorEmbeddedInfo.GetCover(metadata));
    }

    [TestMethod]
    public void FromMetadata_FallsBackToModelSpecThumbnail()
    {
        var info = SafetensorEmbeddedInfo.FromMetadata(
            new Dictionary<string, string>
            {
                ["modelspec.thumbnail"] = "data:image/png;base64," + Convert.ToBase64String(GalleryJpegBytes),
            }
        );

        CollectionAssert.AreEqual(GalleryJpegBytes, info.Cover);
    }

    [TestMethod]
    public void FromMetadata_PrefersLoraContainerCoverOverModelSpecThumbnail()
    {
        var info = SafetensorEmbeddedInfo.FromMetadata(
            new Dictionary<string, string>
            {
                ["modelspec.thumbnail"] = "data:image/png;base64," + Convert.ToBase64String(GalleryJpegBytes),
                ["cover_count"] = "1",
                ["cover_1"] = Convert.ToBase64String(CoverJpegBytes),
            }
        );

        CollectionAssert.AreEqual(CoverJpegBytes, info.Cover);
    }

    [TestMethod]
    [DataRow("stable-diffusion-v1/lora", null, "SD 1.5")]
    [DataRow("stable-diffusion-xl-v1-base/lora", null, "SDXL 1.0")]
    [DataRow("flux-1-dev/lora", null, "Flux.1 D")]
    [DataRow("flux-1-schnell", null, "Flux.1 S")]
    [DataRow("stable-diffusion-v3.5-large/lora", null, "SD 3.5 Large")]
    [DataRow("flux-1-dev/lora", "sdxl_base_v1-0", "Flux.1 D")]
    [DataRow(null, "sd_v1", "SD 1.5")]
    [DataRow(null, "sdxl_base_v1-0", "SDXL 1.0")]
    [DataRow("some-unknown-architecture/lora", "sdxl_base_v1-0", "SDXL 1.0")]
    [DataRow(null, "flux1", null)]
    [DataRow(null, "sd_v2", null)]
    public void GetBaseModel_MapsArchitectureToCivitBaseModel(
        string? architecture,
        string? kohyaBaseModel,
        string? expected
    )
    {
        var metadata = new Dictionary<string, string>();
        if (architecture is not null)
            metadata["modelspec.architecture"] = architecture;
        if (kohyaBaseModel is not null)
            metadata["ss_base_model_version"] = kohyaBaseModel;

        Assert.AreEqual(expected, SafetensorEmbeddedInfo.GetBaseModel(metadata));
    }

    [TestMethod]
    public void GetTrainedWords_SplitsTriggerPhrase()
    {
        var metadata = new Dictionary<string, string>
        {
            ["modelspec.trigger_phrase"] = "mychar, red jacket,, mychar ",
        };

        CollectionAssert.AreEqual(
            new[] { "mychar", "red jacket" },
            SafetensorEmbeddedInfo.GetTrainedWords(metadata)
        );
    }

    [TestMethod]
    [DataRow("not-a-hash")]
    [DataRow("0123456789abcdef")]
    [DataRow("zz23456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void GetOriginalSha256_IgnoresInvalidHashes(string value)
    {
        var metadata = new Dictionary<string, string> { ["model_sha256_original"] = value };

        Assert.IsNull(SafetensorEmbeddedInfo.GetOriginalSha256(metadata));
    }

    [TestMethod]
    public void GetConnectedModelInfo_ReturnsNull_WhenJsonIsInvalid()
    {
        var metadata = new Dictionary<string, string> { ["cm_info"] = "{not json" };

        Assert.IsNull(SafetensorEmbeddedInfo.GetConnectedModelInfo(metadata));
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
