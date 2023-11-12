using Refit;
using StabilityMatrix.Core.Models.Api.CivitTRPC;

namespace StabilityMatrix.Core.Api;

[Headers("Referer: https://civitai.com")]
public interface ICivitTRPCApi
{
    [Headers("Content-Type: application/x-www-form-urlencoded")]
    [Get("/api/trpc/userProfile.get")]
    Task<CivitUserProfileResponse> GetUserProfile(
        [Query] CivitUserProfileRequest input,
        [Authorize] string bearerToken,
        CancellationToken cancellationToken = default
    );
}
