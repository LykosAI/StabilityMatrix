using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Python;

public readonly record struct PipPackageInfo(
    string Name,
    string Version,
    string? EditableProjectLocation = null
);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(PipPackageInfo))]
internal partial class PipPackageInfoSerializerContext : JsonSerializerContext;
