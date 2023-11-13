using Refit;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface ILykosAuthApi
{
    [Headers("Authorization: Bearer")]
    [Get("/api/Users/{email}")]
    Task<GetUserResponse> GetUser(string email, CancellationToken cancellationToken = default);

    [Post("/api/Login")]
    Task<PostLoginResponse> PostLogin(
        [Body] PostLoginRequest request,
        CancellationToken cancellationToken = default
    );
}
