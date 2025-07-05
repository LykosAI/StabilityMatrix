using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitModelFpType>))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CivitModelFpType
{
    bf16,
    fp16,
    fp32,
    tf32,
    fp8,
    nf4,
}
