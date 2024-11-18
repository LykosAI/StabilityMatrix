using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(DefaultUnknownEnumConverter<CivitFileType>))]
public enum CivitFileType
{
    Unknown,
    Model,
    VAE,
    Config,
    Archive,

    [EnumMember(Value = "Pruned Model")]
    PrunedModel,

    [EnumMember(Value = "Training Data")]
    TrainingData
}
