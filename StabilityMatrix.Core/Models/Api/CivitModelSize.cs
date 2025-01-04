using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

[JsonConverter(typeof(JsonStringEnumConverter<CivitModelSize>))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum CivitModelSize
{
    full,
    pruned,
}
