using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Packages;

public class PythonPackageOptions
{
    [JsonConverter(typeof(JsonStringEnumConverter<TorchIndex>))]
    public TorchIndex? TorchIndex { get; set; }

    public string? TorchVersion { get; set; }
}
