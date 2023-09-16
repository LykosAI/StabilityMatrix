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

    private static readonly byte[] Idat = { 0x49, 0x44, 0x41, 0x54 };
    private static readonly byte[] Text = { 0x74, 0x45, 0x58, 0x74 };

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
        // skip to end of png header stuff
        byteStream.BaseStream.Position = 0x21;
        while (byteStream.BaseStream.Position < byteStream.BaseStream.Length)
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

            // skip crc
            byteStream.BaseStream.Position += 4;
        }

        return string.Empty;
    }
}
