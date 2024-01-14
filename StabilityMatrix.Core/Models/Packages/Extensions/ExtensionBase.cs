using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public abstract class ExtensionBase
{
    public string ByAuthor => $"By {Author}";

    public abstract string RepoName { get; }
    public abstract string DisplayName { get; set; }
    public abstract string Author { get; }

    public abstract string Blurb { get; }
    public abstract IEnumerable<string> CompatibleWith { get; }
    public abstract string MainBranch { get; }

    public abstract Task InstallExtensionAsync(
        DirectoryPath installDirectory,
        string branch,
        CancellationToken cancellationToken = default
    );

    public string GithubUrl => $"https://github.com/{Author}/{RepoName}";
}
