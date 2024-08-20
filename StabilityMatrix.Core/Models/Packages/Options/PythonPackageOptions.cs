using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Packages;

public class PythonPackageOptions
{
    [JsonConverter(typeof(JsonStringEnumConverter<TorchVersion>))]
    public TorchVersion? TorchVersion { get; set; }
}
