using Refit;
using StabilityMatrix.Core.Models.Api.CivitTRPC;

namespace StabilityMatrix.Core.Api;

[Headers(
    "Content-Type: application/x-www-form-urlencoded",
    "Referer: https://civitai.com",
    "Origin: https://civitai.com"
)]
public interface ICivitTRPCApi
{
    [QueryUriFormat(UriFormat.UriEscaped)]
    [Get("/api/trpc/userProfile.get")]
    Task<CivitUserProfileResponse> GetUserProfile(
        [Query] CivitUserProfileRequest input,
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );

    [QueryUriFormat(UriFormat.UriEscaped)]
    [Get("/api/trpc/buzz.getUserAccount")]
    Task<CivitTrpcResponse<CivitUserAccountResponse>> GetUserAccount(
        [Query] string input,
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );

    Task<CivitTrpcResponse<CivitUserAccountResponse>> GetUserAccountDefault(
        string bearerToken,
        CancellationToken cancellationToken = default
    )
    {
        return GetUserAccount(
            "{\"json\":null,\"meta\":{\"values\":[\"undefined\"]}}",
            bearerToken,
            cancellationToken
        );
    }

    [QueryUriFormat(UriFormat.UriEscaped)]
    [Get("/api/trpc/user.getById")]
    Task<CivitTrpcResponse<CivitGetUserByIdResponse>> GetUserById(
        [Query] CivitGetUserByIdRequest input,
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );

    [Post("/api/trpc/user.toggleFavoriteModel")]
    Task<HttpResponseMessage> ToggleFavoriteModel(
        [Body] CivitUserToggleFavoriteModelRequest request,
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );

    [QueryUriFormat(UriFormat.UriEscaped)]
    [Get("/api/trpc/image.getGenerationData")]
    Task<CivitTrpcResponse<CivitImageGenerationDataResponse>> GetImageGenerationData(
        [Query] string input,
        CancellationToken cancellationToken = default
    );
}
