using System;
using StabilityMatrix.Avalonia.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockDiscordRichPresenceService : IDiscordRichPresenceService
{
    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public void UpdateState()
    {
    }
}
