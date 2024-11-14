namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public class OpenModelDbKeyedModel : OpenModelDbModel
{
    public required string Id { get; set; }

    public OpenModelDbKeyedModel(OpenModelDbModel model)
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
    }
}
