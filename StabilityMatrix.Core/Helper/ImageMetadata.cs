using System.Text;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Png;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using Directory = MetadataExtractor.Directory;

namespace StabilityMatrix.Core.Helper;

public class ImageMetadata
{
    private IReadOnlyList<Directory>? Directories { get; set; }

    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Idat = "IDAT"u8.ToArray();
    private static readonly byte[] Text = "tEXt"u8.ToArray();

    public static ImageMetadata ParseFile(FilePath path)
    {
        return new ImageMetadata { Directories = ImageMetadataReader.ReadMetadata(path) };
    }

    public static ImageMetadata ParseFile(Stream stream)
    {
        return new ImageMetadata { Directories = ImageMetadataReader.ReadMetadata(stream) };
    }

    public System.Drawing.Size? GetImageSize()
    {
        if (Directories?.OfType<PngDirectory>().FirstOrDefault() is { } header)
        {
            header.TryGetInt32(PngDirectory.TagImageWidth, out var width);
            header.TryGetInt32(PngDirectory.TagImageHeight, out var height);

            return new System.Drawing.Size(width, height);
        }

        return null;
    }

    public static System.Drawing.Size GetImageSize(byte[] inputImage)
    {
        var imageWidthBytes = inputImage[0x10..0x14];
        var imageHeightBytes = inputImage[0x14..0x18];
        var imageWidth = BitConverter.ToInt32(imageWidthBytes.Reverse().ToArray());
        var imageHeight = BitConverter.ToInt32(imageHeightBytes.Reverse().ToArray());

        return new System.Drawing.Size(imageWidth, imageHeight);
    }

    public static System.Drawing.Size GetImageSize(BinaryReader reader)
    {
        var oldPosition = reader.BaseStream.Position;

        reader.BaseStream.Position = 0x10;
        var imageWidthBytes = reader.ReadBytes(4);
        var imageHeightBytes = reader.ReadBytes(4);

        var imageWidth = BitConverter.ToInt32(imageWidthBytes.Reverse().ToArray());
        var imageHeight = BitConverter.ToInt32(imageHeightBytes.Reverse().ToArray());

        reader.BaseStream.Position = oldPosition;

        return new System.Drawing.Size(imageWidth, imageHeight);
    }

    public static (
        string? Parameters,
        string? ParametersJson,
        string? SMProject,
        string? ComfyNodes
    ) GetAllFileMetadata(FilePath filePath)
    {
        using var stream = filePath.Info.OpenRead();
        using var reader = new BinaryReader(stream);

        var parameters = ReadTextChunk(reader, "parameters");
        var parametersJson = ReadTextChunk(reader, "parameters-json");
        var smProject = ReadTextChunk(reader, "smproj");
        var comfyNodes = ReadTextChunk(reader, "prompt");

        return (
            string.IsNullOrEmpty(parameters) ? null : parameters,
            string.IsNullOrEmpty(parametersJson) ? null : parametersJson,
            string.IsNullOrEmpty(smProject) ? null : smProject,
            string.IsNullOrEmpty(comfyNodes) ? null : comfyNodes
        );
    }

    public IEnumerable<Tag>? GetTextualData()
    {
        // Get the PNG-tEXt directory
        return Directories
            ?.Where(d => d.Name == "PNG-tEXt")
            .SelectMany(d => d.Tags)
            .Where(t => t.Name == "Textual Data");
    }

    public GenerationParameters? GetGenerationParameters()
    {
        var textualData = GetTextualData()?.ToArray();
        if (textualData is null)
        {
            return null;
        }

        // Use "parameters-json" tag if exists
        if (
            textualData.FirstOrDefault(
                tag => tag.Description is { } desc && desc.StartsWith("parameters-json: ")
            ) is
            { Description: { } description }
        )
        {
            description = description.StripStart("parameters-json: ");

            return JsonSerializer.Deserialize<GenerationParameters>(description);
        }

        // Otherwise parse "parameters" tag
        if (
            textualData.FirstOrDefault(
                tag => tag.Description is { } desc && desc.StartsWith("parameters: ")
            ) is
            { Description: { } parameters }
        )
        {
            parameters = parameters.StripStart("parameters: ");

            if (GenerationParameters.TryParse(parameters, out var generationParameters))
            {
                return generationParameters;
            }
        }

        return null;
    }

    public static string ReadTextChunk(BinaryReader byteStream, string key)
    {
        byteStream.BaseStream.Position = 0;

        // Read first 8 bytes and make sure they match the png header
        if (!byteStream.ReadBytes(8).SequenceEqual(PngHeader))
        {
            return string.Empty;
        }

        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length - 4)
        {
            var chunkSize = BitConverter.ToInt32(byteStream.ReadBytes(4).Reverse().ToArray());
            var chunkType = Encoding.UTF8.GetString(byteStream.ReadBytes(4));

            if (chunkType == Encoding.UTF8.GetString(Idat))
            {
                return string.Empty;
            }

            if (chunkType == Encoding.UTF8.GetString(Text))
            {
                var textBytes = byteStream.ReadBytes(chunkSize);
                var text = Encoding.UTF8.GetString(textBytes);
                if (text.StartsWith($"{key}\0"))
                {
                    return text[(key.Length + 1)..];
                }
            }
            else
            {
                // skip chunk data
                byteStream.BaseStream.Position += chunkSize;
            }

            // skip crc
            byteStream.BaseStream.Position += 4;
        }

        return string.Empty;
    }

    public static MemoryStream? BuildImageWithoutMetadata(FilePath imagePath)
    {
        using var byteStream = new BinaryReader(File.OpenRead(imagePath));
        byteStream.BaseStream.Position = 0;

        if (!byteStream.ReadBytes(8).SequenceEqual(PngHeader))
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        memoryStream.Write(PngHeader);

        // add the IHDR chunk
        var ihdrStuff = byteStream.ReadBytes(25);
        memoryStream.Write(ihdrStuff);

        // find IDATs
        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length - 4)
        {
            var chunkSizeBytes = byteStream.ReadBytes(4);
            var chunkSize = BitConverter.ToInt32(chunkSizeBytes.Reverse().ToArray());
            var chunkTypeBytes = byteStream.ReadBytes(4);
            var chunkType = Encoding.UTF8.GetString(chunkTypeBytes);

            if (chunkType != Encoding.UTF8.GetString(Idat))
            {
                // skip chunk data
                byteStream.BaseStream.Position += chunkSize;
                // skip crc
                byteStream.BaseStream.Position += 4;
                continue;
            }

            memoryStream.Write(chunkSizeBytes);
            memoryStream.Write(chunkTypeBytes);
            var idatBytes = byteStream.ReadBytes(chunkSize);
            memoryStream.Write(idatBytes);
            var crcBytes = byteStream.ReadBytes(4);
            memoryStream.Write(crcBytes);
        }

        // Add IEND chunk
        memoryStream.Write([0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82]);
        memoryStream.Position = 0;
        return memoryStream;
    }
}
