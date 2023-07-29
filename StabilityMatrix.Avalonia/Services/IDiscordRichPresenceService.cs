using System;

namespace StabilityMatrix.Avalonia.Services;

public interface IDiscordRichPresenceService : IDisposable
{
    public void Initialize();
}
