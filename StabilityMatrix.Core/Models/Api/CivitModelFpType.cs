using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;


[JsonConverter(typeof(JsonStringEnumConverter))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CivitModelFpType
{
    fp16,
    fp32
}
