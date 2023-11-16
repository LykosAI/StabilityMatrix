using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.CivitTRPC;

public record CivitUserAccountResponse(int Id, int Balance, int LifetimeBalance);

public record CivitTrpcResponse<T>
{
    [JsonPropertyName("result")]
    public required CivitTrpcResponseData<T> Result { get; set; }

    public record CivitTrpcResponseData<TData>
    {
        [JsonPropertyName("data")]
        public required CivitTrpcResponseDataJson<TData> Data { get; set; }
    }

    public record CivitTrpcResponseDataJson<TJson>
    {
        [JsonPropertyName("Json")]
        public required TJson Json { get; set; }
    }
}
