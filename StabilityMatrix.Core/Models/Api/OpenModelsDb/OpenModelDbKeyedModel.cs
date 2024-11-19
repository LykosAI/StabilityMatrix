namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public record OpenModelDbKeyedModel : OpenModelDbModel
{
    public required string Id { get; set; }

    public OpenModelDbKeyedModel() { }

    public OpenModelDbKeyedModel(OpenModelDbModel model)
        : base(model) { }

    public OpenModelDbKeyedModel(OpenModelDbKeyedModel model)
        : base(model)
    {
        Id = model.Id;
    }

    public SharedFolderType? GetSharedFolderType()
    {
        return Architecture?.ToLowerInvariant() switch
        {
            "esrgan" => SharedFolderType.ESRGAN,
            _ => null
        };
    }
}
