namespace StabilityMatrix.Core.Models.Api; // Or StabilityMatrix.Core.Models.Api.HuggingFace

public class HuggingFaceAccountStatusUpdateEventArgs : EventArgs
{
    public bool IsConnected { get; init; }
    public string? Username { get; init; } // Optional: if we decide to fetch/display username

    public static HuggingFaceAccountStatusUpdateEventArgs Disconnected => new() { IsConnected = false };

    // Constructor to allow initialization, matching the usage in AccountSettingsViewModel
    public HuggingFaceAccountStatusUpdateEventArgs() { }

    public HuggingFaceAccountStatusUpdateEventArgs(bool isConnected, string? username)
    {
        IsConnected = isConnected;
        Username = username;
    }
}
