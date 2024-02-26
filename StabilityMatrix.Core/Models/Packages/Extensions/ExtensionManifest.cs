namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record ExtensionManifest(Uri Uri)
{
    public static implicit operator ExtensionManifest(string uri) => new(new Uri(uri, UriKind.Absolute));
}
