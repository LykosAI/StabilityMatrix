namespace StabilityMatrix.Core.Models.Documentation;

/// <summary>
/// Central location for the documentation source repository coordinates.
/// The in-app documentation viewer fetches its content from this repo's <c>docs/</c> folder.
/// </summary>
public static class DocumentationConstants
{
    /// <summary>GitHub repository owner.</summary>
    public const string Owner = "LykosAI";

    /// <summary>GitHub repository name.</summary>
    public const string Repo = "StabilityMatrix";

    /// <summary>Branch that the docs are read from.</summary>
    public const string Branch = "main";

    /// <summary>Root folder within the repository that contains the documentation.</summary>
    public const string DocsRoot = "docs";

    /// <summary>
    /// Base URL for raw file content, e.g.
    /// <c>https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/</c>.
    /// </summary>
    public static string RawBaseUrl => $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/";

    /// <summary>
    /// Builds the raw content URL for a path relative to the repository root
    /// (e.g. <c>docs/getting-started/overview.md</c>).
    /// </summary>
    public static string GetRawUrl(string repoRelativePath) => RawBaseUrl + repoRelativePath.TrimStart('/');
}
