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

    public string? DisplayName =>
        Principal?.GetClaim(OpenIddictConstants.Claims.PreferredUsername) ?? Principal?.Identity?.Name;

    public bool IsPatreonConnected => User?.PatreonId != null;
}
