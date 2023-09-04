using System.Collections.Generic;
using System.Linq;
using MetadataExtractor;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Helpers;

public class ImageMetadata
{
    private IReadOnlyList<Directory>? Directories { get; set; }

    public static ImageMetadata ParseFile(FilePath path)
    {
        return new ImageMetadata() { Directories = ImageMetadataReader.ReadMetadata(path) };
    }

    public string? GetComfyMetadata()
    {
        if (Directories is null)
        {
            return null;
        }

        // For Comfy, we want the PNG-tEXt directory
        if (Directories.FirstOrDefault(d => d.Name == "PNG-tEXt") is not { } pngText)
        {
            return null;
        }

        // Expect the 'Textual Data' tag
        if (
            pngText.Tags.FirstOrDefault(tag => tag.Name == "Textual Data") is not { } textTag
            || textTag.Description is null
        )
        {
            return null;
        }

        // Strip `prompt: ` and the rest of the description is json

        return textTag.Description.StripStart("prompt:").TrimStart();
    }
}
