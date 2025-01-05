namespace StabilityMatrix.Core.Models.Api.OpenModelsDb;

public class OpenModelDbModelsResponse : Dictionary<string, OpenModelDbModel>
{
    public ParallelQuery<OpenModelDbKeyedModel> GetKeyedModels()
    {
        return this.AsParallel().Select(kv => new OpenModelDbKeyedModel(kv.Value) { Id = kv.Key });
    }
}
