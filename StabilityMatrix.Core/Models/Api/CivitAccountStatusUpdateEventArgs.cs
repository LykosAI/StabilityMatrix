using StabilityMatrix.Core.Models.Api.CivitTRPC;

namespace StabilityMatrix.Core.Models.Api;

public class CivitAccountStatusUpdateEventArgs : EventArgs
{
    public static CivitAccountStatusUpdateEventArgs Disconnected { get; } = new();

    public bool IsConnected { get; init; }

    public CivitUserProfileResponse? UserProfile { get; init; }

    public string? UsernameWithParentheses =>
        string.IsNullOrEmpty(UserProfile?.Username) ? null : $"({UserProfile.Username})";
}
