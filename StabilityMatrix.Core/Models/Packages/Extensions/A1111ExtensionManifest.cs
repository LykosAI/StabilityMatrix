using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record A1111ExtensionManifest
{
    public required IEnumerable<ManifestEntry> Extensions { get; init; }

    public IEnumerable<PackageExtension> GetPackageExtensions()
    {
        return Extensions.Select(
            x =>
                new PackageExtension
                {
                    Author = x.FullName?.Split('/').FirstOrDefault() ?? "Unknown",
                    Title = x.Name,
                    Reference = x.Url,
                    Files = [x.Url],
                    Description = x.Description,
                    InstallType = "git-clone"
                }
        );
    }

    public record ManifestEntry
    {
        public string? FullName { get; init; }

        public required string Name { get; init; }

        public required Uri Url { get; init; }

        public string? Description { get; init; }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(A1111ExtensionManifest))]
internal partial class A1111ExtensionManifestSerializerContext : JsonSerializerContext;
