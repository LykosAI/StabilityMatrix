using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Documentation;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Fetches and caches the in-app documentation content from the docs repository.
/// </summary>
public interface IDocumentationService
{
    /// <summary>
    /// Gets the documentation navigation tree, grouped into sections.
    /// The root <c>README.md</c> (if present) is returned as the first entry of the
    /// root section so callers can treat it as the landing page.
    /// </summary>
    /// <exception cref="DocumentationNotAvailableException">
    /// Thrown when the docs folder does not exist in the source repository.
    /// </exception>
    Task<IReadOnlyList<DocumentationSection>> GetSectionsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the raw markdown for a page path relative to the docs root
    /// (e.g. <c>getting-started/overview.md</c>).
    /// </summary>
    Task<string> GetPageMarkdownAsync(
        string docsRelativePath,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default
    );
}
