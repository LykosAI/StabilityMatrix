namespace StabilityMatrix.Core.Models.Api;

public class GeminiAccountStatusUpdateEventArgs : EventArgs
{
    public bool IsConnected { get; init; }
    public string? ErrorMessage { get; init; }

    public static GeminiAccountStatusUpdateEventArgs Disconnected => new(false);

    public GeminiAccountStatusUpdateEventArgs() { }

    public GeminiAccountStatusUpdateEventArgs(bool isConnected, string? errorMessage = null)
    {
        IsConnected = isConnected;
        ErrorMessage = errorMessage;
    }
}
