using System.Security.Claims;
using OpenIddict.Abstractions;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Api.LykosAuthApi;

namespace StabilityMatrix.Core.Models.Api.Lykos;

public class LykosAccountStatusUpdateEventArgs : EventArgs
{
    public static LykosAccountStatusUpdateEventArgs Disconnected { get; } = new();

    public bool IsConnected { get; init; }

    public ClaimsPrincipal? Principal { get; init; }

    public AccountResponse? User { get; init; }

    public string? Id => Principal?.GetClaim(OpenIddictConstants.Claims.Subject);

    public string? DisplayName =>
        Principal?.GetClaim(OpenIddictConstants.Claims.PreferredUsername) ?? Principal?.Identity?.Name;

    public string? Email => Principal?.GetClaim(OpenIddictConstants.Claims.Email);

    public bool IsPatreonConnected => User?.PatreonId != null;
}
