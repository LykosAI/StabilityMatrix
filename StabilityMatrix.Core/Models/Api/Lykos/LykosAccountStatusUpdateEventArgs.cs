namespace StabilityMatrix.Core.Models.Api.Lykos;

public class LykosAccountStatusUpdateEventArgs : EventArgs
{
    public static LykosAccountStatusUpdateEventArgs Disconnected { get; } = new();

    public bool IsConnected { get; init; }

    public GetUserResponse? User { get; init; }

    public bool IsPatreonConnected => User?.PatreonId != null;
}
