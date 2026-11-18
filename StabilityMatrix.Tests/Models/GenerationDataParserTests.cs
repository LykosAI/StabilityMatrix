using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.GenerationData;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class GenerationDataParserTests
{
    private const string ComfyPrompt = """
        {
          "3": {"class_type": "KSampler", "inputs": {"seed": 123456789012345, "steps": 25, "cfg": 6.5,
                "sampler_name": "euler_ancestral", "scheduler": "normal", "denoise": 1.0,
                "model": ["4", 0], "positive": ["6", 0], "negative": ["7", 0], "latent_image": ["5", 0]}},
          "4": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "sdxl.safetensors"}},
          "5": {"class_type": "EmptyLatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
          "6": {"class_type": "CLIPTextEncode", "inputs": {"text": "a cat in a hat", "clip": ["4", 1]}},
          "7": {"class_type": "CLIPTextEncode", "inputs": {"text": "blurry", "clip": ["4", 1]}},
          "9": {"class_type": "SaveImage", "inputs": {"filename_prefix": "ComfyUI", "images": ["8", 0]}}
        }
        """;

    private const string A1111Parameters = """
        a cat in a hat
        Negative prompt: blurry
        Steps: 20, Sampler: DPM++ 2M, Schedule type: Karras, CFG scale: 7, Seed: 42, Size: 832x1216, Model hash: abc123, Model: sdxl, Version: neo-2.4
        """;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static GenerationDataInfo? ParsePng(params (string Type, byte[] Data)[] chunks)
    {
        using var stream = new MemoryStream(BuildPng(chunks));
        return GenerationDataParser.Parse(MetadataEntries.Read(stream));
    }

    [TestMethod]
    public void Png_ComfyUISaveImage_ReadsPromptAndSettings()
    {
        var info = ParsePng(TextChunk("prompt", ComfyPrompt));

        Assert.IsNotNull(info);
        Assert.AreEqual(GenerationDataSource.ComfyUI, info.Source);
        Assert.AreEqual("ComfyUI", info.DisplayName);
        Assert.IsNotNull(info.Parameters);
        Assert.AreEqual("a cat in a hat", info.Parameters.PositivePrompt);
        Assert.AreEqual("blurry", info.Parameters.NegativePrompt);
        Assert.AreEqual("sdxl.safetensors", info.Parameters.ModelName);
        Assert.AreEqual(123456789012345UL, info.Parameters.Seed);
        Assert.AreEqual(25, info.Parameters.Steps);
        Assert.AreEqual(6.5, info.Parameters.CfgScale);
        Assert.AreEqual("euler_ancestral", info.Parameters.Sampler);
    }

    [TestMethod]
    public void Png_ComfyUIPromptWithNaN_IsRecognized()
    {
        var prompt = ComfyPrompt.Replace("\"denoise\": 1.0", "\"denoise\": NaN");

        var info = ParsePng(InternationalTextChunk("prompt", prompt, compressed: true));

        Assert.AreEqual(GenerationDataSource.ComfyUI, info?.Source);
        Assert.AreEqual("a cat in a hat", info?.Parameters?.PositivePrompt);
    }

    [TestMethod]
    public void Png_ComfyUILinkedSeed_FollowsPrimitiveNode()
    {
        var prompt = ComfyPrompt
            .Replace("\"seed\": 123456789012345", "\"seed\": [\"10\", 0]")
            .Replace(
                "\"9\":",
                "\"10\": {\"class_type\": \"PrimitiveInt\", \"inputs\": {\"value\": 777}},\n\"9\":"
            );

        var info = ParsePng(TextChunk("prompt", prompt));

        Assert.AreEqual(777UL, info?.Parameters?.Seed);
    }

    [TestMethod]
    public void Png_A1111Parameters_NamesForgeNeo()
    {
        var info = ParsePng(TextChunk("parameters", A1111Parameters));

        Assert.IsNotNull(info);
        Assert.AreEqual(GenerationDataSource.Automatic1111, info.Source);
        Assert.AreEqual("Forge Neo", info.DisplayName);
        Assert.AreEqual("a cat in a hat", info.Parameters?.PositivePrompt);
        Assert.AreEqual("blurry", info.Parameters?.NegativePrompt);
        Assert.AreEqual(42UL, info.Parameters?.Seed);
        Assert.AreEqual(832, info.Parameters?.Width);
    }

    [TestMethod]
    public void Png_A1111ParametersNextToComfyGraph_NamesComfyUI()
    {
        var parameters = A1111Parameters.Replace(", Version: neo-2.4", "");

        var info = ParsePng(TextChunk("parameters", parameters), TextChunk("prompt", ComfyPrompt));

        Assert.AreEqual(GenerationDataSource.Automatic1111, info?.Source);
        Assert.AreEqual("ComfyUI", info?.DisplayName);
    }

    [TestMethod]
    public void Png_SwarmUI_IsPreferredOverOtherFormats()
    {
        const string swarm = """
            {"sui_image_params": {"prompt": "a dog", "negativeprompt": "cat", "model": "flux", "seed": 7,
             "steps": 30, "cfgscale": 4.5, "sampler": "euler"}, "sui_models": []}
            """;

        var info = ParsePng(CompressedTextChunk("parameters", swarm), TextChunk("prompt", ComfyPrompt));

        Assert.IsNotNull(info);
        Assert.AreEqual(GenerationDataSource.SwarmUI, info.Source);
        Assert.AreEqual("a dog", info.Parameters?.PositivePrompt);
        Assert.AreEqual("cat", info.Parameters?.NegativePrompt);
        Assert.AreEqual("flux", info.Parameters?.ModelName);
        Assert.AreEqual(30, info.Parameters?.Steps);
        Assert.AreEqual(4.5, info.Parameters?.CfgScale);
    }

    [TestMethod]
    public void Png_InvokeAI_ReadsMetadata()
    {
        const string metadata = """
            {"positive_prompt": "a fox", "negative_prompt": "", "seed": 5, "steps": 30, "cfg_scale": 7.5,
             "scheduler": "euler", "model": {"name": "juggernaut", "base": "sdxl"}}
            """;

        var info = ParsePng(TextChunk("invokeai_metadata", metadata));

        Assert.AreEqual(GenerationDataSource.InvokeAI, info?.Source);
        Assert.AreEqual("a fox", info?.Parameters?.PositivePrompt);
        Assert.IsNull(info?.Parameters?.NegativePrompt);
        Assert.AreEqual("juggernaut", info?.Parameters?.ModelName);
    }

    [TestMethod]
    public void Png_NovelAI_ReadsDescriptionAndComment()
    {
        var info = ParsePng(
            TextChunk("Description", "1girl, solo"),
            TextChunk("Software", "NovelAI"),
            TextChunk("Source", "NovelAI Diffusion V4.5 4BDE2A90"),
            TextChunk(
                "Comment",
                """{"prompt": "1girl, solo", "uc": "lowres", "steps": 28, "scale": 5, "seed": 9, "sampler": "k_euler"}"""
            )
        );

        Assert.AreEqual(GenerationDataSource.NovelAI, info?.Source);
        Assert.AreEqual("1girl, solo", info?.Parameters?.PositivePrompt);
        Assert.AreEqual("lowres", info?.Parameters?.NegativePrompt);
        Assert.AreEqual("NovelAI Diffusion V4.5", info?.Parameters?.ModelName);
    }

    [TestMethod]
    public void Png_WithoutGenerationData_ReturnsNull()
    {
        Assert.IsNull(ParsePng(TextChunk("Software", "GIMP 2.10")));
        Assert.IsNull(ParsePng());
    }

    [TestMethod]
    public void Png_XmpDigitalSourceType_IsLabelledAsAiMade()
    {
        const string xmp = """
            <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description xmlns:Iptc4xmpExt="http://iptc.org/std/Iptc4xmpExt/2008-02-29/"
              Iptc4xmpExt:DigitalSourceType="http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia"/>
            </rdf:RDF></x:xmpmeta>
            """;

        var info = ParsePng(InternationalTextChunk("XML:com.adobe.xmp", xmp, compressed: false));

        Assert.AreEqual(GenerationDataSource.Iptc, info?.Source);
        Assert.IsTrue(info?.IsProvenanceOnly);
        Assert.IsNull(info?.Parameters);
    }

    [TestMethod]
    public void Jpeg_UserCommentUnicode_ReadsA1111Parameters()
    {
        var userComment = "UNICODE\0"u8
            .ToArray()
            .Concat(Encoding.BigEndianUnicode.GetBytes(A1111Parameters))
            .ToArray();
        var jpeg = BuildJpeg(BuildExif(littleEndian: true, (0x9286, 7, userComment)));

        using var stream = new MemoryStream(jpeg);
        var info = GenerationDataParser.Parse(MetadataEntries.Read(stream));

        Assert.AreEqual(GenerationDataSource.Automatic1111, info?.Source);
        Assert.AreEqual("Forge Neo", info?.DisplayName);
        Assert.AreEqual("a cat in a hat", info?.Parameters?.PositivePrompt);
    }

    [TestMethod]
    public void Jpeg_ComfyUILabelledExifValue_ReadsPrompt()
    {
        var description = Encoding.UTF8.GetBytes("prompt:" + ComfyPrompt + "\0");
        var jpeg = BuildJpeg(BuildExif(littleEndian: false, (0x010e, 2, description)));

        using var stream = new MemoryStream(jpeg);
        var info = GenerationDataParser.Parse(MetadataEntries.Read(stream));

        Assert.AreEqual(GenerationDataSource.ComfyUI, info?.Source);
        Assert.AreEqual("a cat in a hat", info?.Parameters?.PositivePrompt);
    }

    [TestMethod]
    public void Webp_StabilityMatrixImageDescription_IsRecognized()
    {
        var json = """{"PositivePrompt":"a lighthouse","NegativePrompt":"fog","Steps":12,"Seed":3}""";
        var exif = BuildExif(littleEndian: true, (0x010e, 2, Encoding.UTF8.GetBytes(json + "\0")));
        var webp = BuildRiff("WEBP", ("VP8L", new byte[6]), ("EXIF", exif));

        using var stream = new MemoryStream(webp);
        var info = GenerationDataParser.Parse(MetadataEntries.Read(stream));

        Assert.AreEqual(GenerationDataSource.StabilityMatrix, info?.Source);
        Assert.AreEqual("a lighthouse", info?.Parameters?.PositivePrompt);
        Assert.AreEqual(12, info?.Parameters?.Steps);
    }

    [TestMethod]
    [DataRow("Version: v1.10.1", "Stable Diffusion WebUI")]
    [DataRow("Version: f2.0.1v1.10.1-previous-669-gdfdcbab6", "Forge")]
    [DataRow("Version: classic", "Forge Classic")]
    [DataRow("Version: neo", "Forge Neo")]
    [DataRow("Version: f1.3.0-v1.10.1RC-latest-1038-g19395bf9", "EasyReforge")]
    [DataRow("Version: f1.0.0v2-v1.10.1RC-latest-2498-g5120e679", "reForge")]
    [DataRow("App: SD.Next, Version: 2c4d0751", "SD.Next")]
    [DataRow("Civitai resources: []", "Civitai")]
    [DataRow("ADetailer version: 24.1.0", null)]
    public void DetectA1111Software(string fields, string? expected)
    {
        var parameters = $"prompt\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 1, {fields}";

        Assert.AreEqual(expected, GenerationDataParser.DetectA1111Software(parameters));
    }

    [TestMethod]
    public void LocalImageFile_FromPath_RecordsGenerationSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        File.WriteAllBytes(path, BuildPng([TextChunk("prompt", ComfyPrompt)]));
        try
        {
            var file = LocalImageFile.FromPath(path);

            Assert.AreEqual(GenerationDataSource.ComfyUI, file.GenerationDataSource);
            Assert.AreEqual("ComfyUI", file.GenerationDataLabel);
            Assert.AreEqual("a cat in a hat", file.GenerationParameters?.PositivePrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    #region File builders

    private static (string, byte[]) TextChunk(string keyword, string text) =>
        ("tEXt", [.. Encoding.Latin1.GetBytes(keyword), 0, .. Encoding.UTF8.GetBytes(text)]);

    private static (string, byte[]) CompressedTextChunk(string keyword, string text) =>
        ("zTXt", [.. Encoding.Latin1.GetBytes(keyword), 0, 0, .. Deflate(Encoding.UTF8.GetBytes(text))]);

    private static (string, byte[]) InternationalTextChunk(string keyword, string text, bool compressed)
    {
        var body = Encoding.UTF8.GetBytes(text);
        return (
            "iTXt",
            [
                .. Encoding.Latin1.GetBytes(keyword),
                0,
                (byte)(compressed ? 1 : 0),
                0,
                0,
                0,
                .. compressed ? Deflate(body) : body,
            ]
        );
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    private static byte[] BuildPng((string Type, byte[] Data)[] chunks)
    {
        using var stream = new MemoryStream();
        stream.Write(PngSignature);

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, 64);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 64);
        header[8] = 8;
        header[9] = 6;

        foreach (
            var (type, data) in chunks
                .Prepend(("IHDR", header))
                .Append(("IDAT", new byte[4]))
                .Append(("IEND", []))
        )
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            stream.Write(length);
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            stream.Write(new byte[4]);
        }
        return stream.ToArray();
    }

    private static byte[] BuildExif(bool littleEndian, params (int Tag, int Type, byte[] Value)[] tags)
    {
        void U16(Span<byte> span, int value)
        {
            if (littleEndian)
                BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)value);
            else
                BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)value);
        }

        void U32(Span<byte> span, int value)
        {
            if (littleEndian)
                BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)value);
            else
                BinaryPrimitives.WriteUInt32BigEndian(span, (uint)value);
        }

        var ifdSize = 2 + tags.Length * 12 + 4;
        var tiff = new byte[8 + ifdSize + tags.Sum(t => t.Value.Length)];
        tiff[0] = tiff[1] = (byte)(littleEndian ? 'I' : 'M');
        U16(tiff.AsSpan(2), 42);
        U32(tiff.AsSpan(4), 8);
        U16(tiff.AsSpan(8), tags.Length);

        var valueOffset = 8 + ifdSize;
        for (var i = 0; i < tags.Length; i++)
        {
            var (tag, type, value) = tags[i];
            var entry = tiff.AsSpan(10 + i * 12);
            U16(entry, tag);
            U16(entry[2..], type);
            U32(entry[4..], value.Length);
            U32(entry[8..], valueOffset);
            value.CopyTo(tiff, valueOffset);
            valueOffset += value.Length;
        }

        return [.. "Exif\0\0"u8, .. tiff];
    }

    private static byte[] BuildJpeg(byte[] exif)
    {
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(exif.Length + 2));
        return [0xFF, 0xD8, 0xFF, 0xE1, .. length, .. exif, 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9];
    }

    private static byte[] BuildRiff(string format, params (string Type, byte[] Data)[] chunks)
    {
        using var body = new MemoryStream();
        body.Write(Encoding.ASCII.GetBytes(format));
        foreach (var (type, data) in chunks)
        {
            Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)data.Length);
            body.Write(Encoding.ASCII.GetBytes(type));
            body.Write(size);
            body.Write(data);
            if (data.Length % 2 == 1)
                body.WriteByte(0);
        }

        var riffSize = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(riffSize, (uint)body.Length);
        return [.. "RIFF"u8, .. riffSize, .. body.ToArray()];
    }

    #endregion
}
