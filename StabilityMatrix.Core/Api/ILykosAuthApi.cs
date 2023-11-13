using Refit;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface ILykosAuthApi
{
    [Headers("Authorization: Bearer")]
    [Get("/api/Users/{email}")]
    Task<GetUserResponse> GetUser(string email, CancellationToken cancellationToken = default);

    [Headers("Authorization: Bearer")]
    [Get("/api/Users/me")]
    Task<GetUserResponse> GetUserSelf(CancellationToken cancellationToken = default);

    [Post("/api/Accounts")]
    Task<LykosAccountTokens> PostAccount(
        [Body] PostAccountRequest request,
        CancellationToken cancellationToken = default
    );

    [Post("/api/Login")]
    Task<LykosAccountTokens> PostLogin(
        [Body] PostLoginRequest request,
        CancellationToken cancellationToken = default
    );
}
