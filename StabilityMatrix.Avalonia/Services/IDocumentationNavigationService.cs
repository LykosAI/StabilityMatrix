namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Opens the in-app documentation viewer, optionally at a specific page.
/// </summary>
public interface IDocumentationNavigationService
{
    /// <summary>
    /// Navigates to the documentation viewer.
    /// </summary>
    /// <param name="docsRelativePath">
    /// Page path relative to the docs root, e.g. <c>advanced/environment-variables.md</c>.
    /// When null, the viewer opens on its landing page.
    /// </param>
    /// <param name="anchor">Optional heading slug to scroll to within the page.</param>
    void OpenDocumentation(string? docsRelativePath = null, string? anchor = null);
}
