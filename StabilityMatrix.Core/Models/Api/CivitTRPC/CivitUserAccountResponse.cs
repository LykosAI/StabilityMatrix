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

/// <summary>
/// Like CivitTrpcResponse, but wrapped as the first item of an array.
/// </summary>
/// <typeparam name="T"></typeparam>
public record CivitTrpcArrayResponse<T>
{
    [JsonPropertyName("result")]
    public required CivitTrpcResponseData<T> Result { get; set; }

    [JsonIgnore]
    public T? InnerJson => Result.Data.Json.FirstOrDefault();

    public record CivitTrpcResponseData<TData>
    {
        [JsonPropertyName("data")]
        public required CivitTrpcResponseDataJson<TData> Data { get; set; }
    }

    public record CivitTrpcResponseDataJson<TJson>
    {
        [JsonPropertyName("Json")]
        public required List<TJson> Json { get; set; }
    }
}
