using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Update;

[JsonSerializable(typeof(UpdateManifest))]
public record UpdateManifest
{
    public required Dictionary<UpdateChannel, UpdatePlatforms> Updates { get; init; }
}


// TODO: Bugged in .NET 7 but we can use in 8 https://github.com/dotnet/runtime/pull/79828
/*[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateManifest))]
public partial class UpdateManifestContext : JsonSerializerContext
{
}*/
