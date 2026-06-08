using System.Text.Json;
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
    /// <summary>
    /// tRPC `model.getById` — the same endpoint the CivitAI website itself uses to load model
    /// detail pages. Used as a fallback when the public REST API returns an empty
    /// `modelVersions` list due to CivitAI's server-side cache desync.
    /// <para>
    /// NOTE: This is an unofficial/internal endpoint. CivitAI actively discourages non-website use
    /// (returns 401 without a Referer header — which our interface-level Headers attribute already
    /// supplies). Treat any 4xx response as a signal that they may have tightened access further.
    /// </para>
    /// </summary>
    [QueryUriFormat(UriFormat.UriEscaped)]
    [Get("/api/trpc/model.getById")]
    Task<CivitTrpcResponse<CivitTRPCModel>> GetModelById(
        [Query] string input,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Convenience wrapper that builds the SuperJSON `input` query string for
    /// <see cref="GetModelById(string,CancellationToken)"/>.
    /// </summary>
    Task<CivitTrpcResponse<CivitTRPCModel>> GetModelById(
        int modelId,
        CancellationToken cancellationToken = default
    )
    {
        var input = JsonSerializer.Serialize(new { json = new { id = modelId } });
        return GetModelById(input, cancellationToken);
    }

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
