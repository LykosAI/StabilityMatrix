using System.Text.Json;
using MetadataExtractor;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using Directory = MetadataExtractor.Directory;

namespace StabilityMatrix.Core.Helper;

public class ImageMetadata
{
    private IReadOnlyList<Directory>? Directories { get; set; }

    public static ImageMetadata ParseFile(FilePath path)
    {
        return new ImageMetadata { Directories = ImageMetadataReader.ReadMetadata(path) };
    }

    public IEnumerable<Tag>? GetTextualData()
    {
        // Get the PNG-tEXt directory
        return Directories?
            .Where(d => d.Name == "PNG-tEXt")
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
}
