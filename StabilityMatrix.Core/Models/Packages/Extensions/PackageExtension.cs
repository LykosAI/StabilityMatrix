namespace StabilityMatrix.Core.Models.Packages.Extensions;

public record PackageExtension
{
    /// <summary>
    /// Comfy Node Registry id (e.g. "comfyui-kjnodes") when the manifest provides one.
    /// Matches the <c>cnr_id</c> that ComfyUI embeds per node in workflow files.
    /// </summary>
    public string? Id { get; init; }

    public required string Author { get; init; }

    public required string Title { get; init; }

    public required Uri Reference { get; init; }

    public required IEnumerable<Uri> Files { get; init; }

    public IEnumerable<string>? Pip { get; init; }

    public string? Description { get; init; }

    public string? InstallType { get; init; }

    public bool IsInstalled { get; init; }
}
