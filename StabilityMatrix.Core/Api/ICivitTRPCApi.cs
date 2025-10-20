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
    Task<CivitTrpcArrayResponse<CivitUserAccountResponse>> GetUserAccount(
        [Query] string input,
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );

    [QueryUriFormat(UriFormat.UriEscaped)]
    [Get("/api/trpc/buzz.getUserAccount")]
    Task<CivitTrpcArrayResponse<CivitUserAccountResponse>> GetUserAccount(
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Calls <see cref="GetUserAccount(string, string, CancellationToken)"/> with default JSON input.
    /// Not required and returns 401 since Oct 2025 since civit changes.
    /// Mainly just use <see cref="GetUserAccount(string, CancellationToken)"/> instead.
    /// </summary>
    Task<CivitTrpcArrayResponse<CivitUserAccountResponse>> GetUserAccountDefault(
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
