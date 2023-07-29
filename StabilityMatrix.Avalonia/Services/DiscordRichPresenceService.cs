using System;
using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Avalonia.Services;

public class DiscordRichPresenceService : IDiscordRichPresenceService
{
    private const string ApplicationId = "1134669805237059615";
    
    private readonly ILogger<DiscordRichPresenceService> logger;
    private DiscordRpcClient? client;
    
    public DiscordRichPresenceService(ILogger<DiscordRichPresenceService> logger)
    {
        this.logger = logger;
    }
    
    public void Initialize()
    {
        if (client != null) return;
        
        client = new DiscordRpcClient(ApplicationId);
        client.Logger = new NullLogger();
        
        client.OnReady += (sender, e) =>
        {
            logger.LogInformation("Received Ready from user {User}", e.User.Username);
        };
        
        client.OnPresenceUpdate += (sender, e) =>
        {
            logger.LogInformation("Received Update: {Presence}", e.Presence.ToString());
        };
        
        // Connect to the RPC
        client.Initialize();
        
        // Set rich presence
        client.SetPresence(new RichPresence
        {
            Details = "Stability Matrix",
            State = "In the main menu",
            Assets = new DiscordRPC.Assets
            {
                LargeImageKey = "stabilitymatrix-logo-1",
                LargeImageText = "Stability Matrix",
                SmallImageKey = "stabilitymatrix-logo-1",
                SmallImageText = "Stability Matrix"
            }
        });
    }

    public void Dispose()
    {
        client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
