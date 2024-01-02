using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public class ComfyManager(IPrerequisiteHelper prerequisiteHelper) : ExtensionBase
{
    public override string RepoName => "ComfyUI-Manager";
    public override string DisplayName { get; set; } = "ComfyUI Manager";
    public override string Author => "ltdrdata";

    public override string Blurb =>
        "ComfyUI-Manager is an extension designed to enhance the usability of ComfyUI. It offers management functions to install, remove, disable, and enable various custom nodes of ComfyUI.";

    public override IEnumerable<string> CompatibleWith => [nameof(ComfyUI)];
    public override string MainBranch => "main";

    public override Task InstallExtensionAsync(
        DirectoryPath installDirectory,
        string branch,
        CancellationToken cancellationToken = default
    )
    {
        return Directory.Exists(Path.Combine(installDirectory, RepoName))
            ? Task.CompletedTask
            : prerequisiteHelper.RunGit(new[] { "clone", GithubUrl }, installDirectory);
    }
}
