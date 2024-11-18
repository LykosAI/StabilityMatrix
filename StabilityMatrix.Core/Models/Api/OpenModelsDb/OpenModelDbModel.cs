using System.Text.Json.Serialization;
using OneOf;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public record OpenModelDbModel
{
    public string? Name { get; set; }

    [JsonConverter(typeof(OneOfJsonConverter<string, string[]>))]
    public OneOf<string, string[]>? Author { get; set; }
    public string? License { get; set; }
    public List<string>? Tags { get; set; }
    public string? Description { get; set; }
    public DateOnly? Date { get; set; }
    public string? Architecture { get; set; }
    public List<string>? Size { get; set; }
    public int? Scale { get; set; }
    public int? InputChannels { get; set; }
    public int? OutputChannels { get; set; }
    public List<OpenModelDbResource>? Resources { get; set; }
    public List<OpenModelDbImage>? Images { get; set; }
    public OpenModelDbImage? Thumbnail { get; set; }

    public OpenModelDbModel() { }

    public OpenModelDbModel(OpenModelDbModel model)
    {
        Name = model.Name;
        Author = model.Author;
        License = model.License;
        Tags = model.Tags;
        Description = model.Description;
        Date = model.Date;
        Architecture = model.Architecture;
        Size = model.Size;
        Scale = model.Scale;
        InputChannels = model.InputChannels;
        OutputChannels = model.OutputChannels;
        Resources = model.Resources;
        Images = model.Images;
        Thumbnail = model.Thumbnail;
    }
}
