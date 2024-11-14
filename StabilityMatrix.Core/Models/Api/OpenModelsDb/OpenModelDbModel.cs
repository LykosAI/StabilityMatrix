using System.Text.Json.Serialization;
using OneOf;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public class OpenModelDbModel
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
}
