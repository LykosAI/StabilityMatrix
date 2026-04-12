using System.Security.Claims;
using OpenIddict.Abstractions;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Api.LykosAuthApi;

namespace StabilityMatrix.Core.Models.Api.Lykos;

public class LykosAccountStatusUpdateEventArgs : EventArgs
{
    private static readonly string[] TierRoles = ["Visionary", "Pioneer", "Insider", "Supporter"];
    private static readonly string[] SpecialRoles = ["Developer", "BetaTester", "Translator"];

    public static LykosAccountStatusUpdateEventArgs Disconnected { get; } = new();

    public bool IsConnected { get; init; }

    public ClaimsPrincipal? Principal { get; init; }

    public AccountResponse? User { get; init; }

    public string? Id => Principal?.GetClaim(OpenIddictConstants.Claims.Subject);

    public string? DisplayName =>
        Principal?.GetClaim(OpenIddictConstants.Claims.PreferredUsername) ?? Principal?.Identity?.Name;

    public string? Email => Principal?.GetClaim(OpenIddictConstants.Claims.Email);

    public bool IsPatreonConnected => User?.PatreonId != null;

    public bool IsActiveSupporter =>
        User?.Roles.Contains("ActivePatron") == true || User?.Roles.Contains("ActiveSubscriber") == true;

    /// <summary>
    /// The highest subscription tier from the user's roles, or null if none.
    /// Priority: Visionary > Pioneer > Insider > Supporter.
    /// </summary>
    public string? HighestTier => TierRoles.FirstOrDefault(r => User?.Roles.Contains(r) == true);

    /// <summary>
    /// All displayable roles (tier + special) the user has, in priority order.
    /// </summary>
    public IReadOnlyList<string> DisplayRoles =>
        TierRoles.Concat(SpecialRoles).Where(r => User?.Roles.Contains(r) == true).ToList();

    /// <summary>
    /// Whether the user has an active Stripe subscription (active or trialing).
    /// </summary>
    public bool HasStripeSubscription => User?.StripeSubscriptionStatus is "active" or "trialing";
}
