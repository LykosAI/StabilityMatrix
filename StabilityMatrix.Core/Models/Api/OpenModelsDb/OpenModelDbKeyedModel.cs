namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public record OpenModelDbKeyedModel : OpenModelDbModel
{
    public required string Id { get; set; }

    public OpenModelDbKeyedModel(OpenModelDbKeyedModel model)
        : base(model)
    {
        Id = model.Id;
    }
}
