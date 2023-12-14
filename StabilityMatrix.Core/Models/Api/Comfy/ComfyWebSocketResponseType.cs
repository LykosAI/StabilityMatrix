using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Api.Comfy;

[JsonConverter(typeof(DefaultUnknownEnumConverter<ComfyWebSocketResponseType>))]
public enum ComfyWebSocketResponseType
{
    Unknown,

    [EnumMember(Value = "status")]
    Status,

    [EnumMember(Value = "execution_start")]
    ExecutionStart,

    [EnumMember(Value = "execution_cached")]
    ExecutionCached,

    [EnumMember(Value = "execution_error")]
    ExecutionError,

    [EnumMember(Value = "executing")]
    Executing,

    [EnumMember(Value = "progress")]
    Progress,

    [EnumMember(Value = "executed")]
    Executed,
}
