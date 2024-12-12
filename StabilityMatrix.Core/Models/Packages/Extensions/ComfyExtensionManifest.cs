using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record ComfyExtensionManifest
{
    public required IEnumerable<ManifestEntry> CustomNodes { get; init; }

    public IEnumerable<PackageExtension> GetPackageExtensions()
    {
        return CustomNodes.Select(
            x =>
                new PackageExtension
                {
                    Author = x.Author,
                    Title = x.Title,
                    Reference = x.Reference,
                    Files = x.Files,
                    Pip = x.Pip,
                    Description = x.Description,
                    InstallType = x.InstallType
                }
        );
    }

    public record ManifestEntry
    {
        public required string Author { get; init; }

        public required string Title { get; init; }

        public required Uri Reference { get; init; }

        public required IEnumerable<Uri> Files { get; init; }

        public IEnumerable<string>? Pip { get; init; }

        public string? Description { get; init; }

        public string? InstallType { get; init; }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ComfyExtensionManifest))]
internal partial class ComfyExtensionManifestSerializerContext : JsonSerializerContext;
