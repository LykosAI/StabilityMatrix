using System;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Defines a remote downloadable resource.
/// </summary>
public readonly record struct RemoteResource
{
    public Uri Url { get; }
    
    public Uri[]? FallbackUrls { get; }
    
    public string HashSha256 { get; }

    public RemoteResource(Uri url, string hashSha256)
    {
        Url = url;
        HashSha256 = hashSha256;
    }
    
    public RemoteResource(Uri[] urls, string hashSha256)
    {
        if (urls.Length == 0)
        {
            throw new ArgumentException("Must have at least one url.", nameof(urls));
        }
        
        Url = urls[0];
        FallbackUrls = urls.Length > 1 ? urls[1..] : null;
        HashSha256 = hashSha256;
    }
}
