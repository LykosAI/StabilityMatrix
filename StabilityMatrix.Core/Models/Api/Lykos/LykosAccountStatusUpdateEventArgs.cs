using System.Security.Claims;
using StabilityMatrix.Core.Api.LykosAuthApi;

namespace StabilityMatrix.Core.Models.Api.Lykos;

public class LykosAccountStatusUpdateEventArgs : EventArgs
{
    public static LykosAccountStatusUpdateEventArgs Disconnected { get; } = new();

    public bool IsConnected { get; init; }

    public ClaimsPrincipal? Principal { get; init; }

    public AccountResponse? User { get; init; }

    public bool IsPatreonConnected => User?.PatreonId != null;
}
