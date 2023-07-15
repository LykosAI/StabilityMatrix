using System;

namespace StabilityMatrix.Avalonia;

internal static class Assets
{
    /// <summary>
    /// Fixed image for models with no images.
    /// </summary>
    public static Uri NoImage { get; } =
        new("avares://StabilityMatrix.Avalonia/Assets/noimage.png");
    
    public static Uri DiscordServerUrl { get; } =
        new("https://discord.com/invite/TUrgfECxHz"); 
    
    public static Uri PatreonUrl { get; } =
        new("https://patreon.com/StabilityMatrix"); 
}
