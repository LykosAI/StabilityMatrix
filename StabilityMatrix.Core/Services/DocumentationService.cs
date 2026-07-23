using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Core.Models.Documentation;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IDocumentationService, DocumentationService>]
public class DocumentationService(
    ILogger<DocumentationService> logger,
    IGitHubClient gitHubClient,
    IHttpClientFactory httpClientFactory
) : IDocumentationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(1);

    private const string TreeCacheFileName = "docs-tree.md";

    private static DirectoryPath CacheDir =>
        new(Path.Combine(Path.GetTempPath(), "StabilityMatrix", "Cache", "Docs"));

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentationSection>> GetSectionsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default
    )
    {
        var cacheFile = CacheDir.JoinFile(TreeCacheFileName);

        // Fetch fresh listing, falling back to any cached copy on failure.
        List<string>? paths = null;

        if (!forceRefresh && IsFresh(cacheFile))
        {
            paths = await ReadCachedPathsAsync(cacheFile, cancellationToken).ConfigureAwait(false);
        }

        if (paths is null)
        {
            try
            {
                paths = await FetchDocsPathsAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await WriteCachedPathsAsync(cacheFile, paths, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception cacheEx) when (cacheEx is not OperationCanceledException)
                {
                    logger.LogWarning(cacheEx, "Failed to write docs tree to cache");
                }
            }
            catch (DocumentationNotAvailableException)
            {
                throw;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Failed to fetch docs tree, attempting to use cached copy");
                paths = await ReadCachedPathsAsync(cacheFile, cancellationToken).ConfigureAwait(false);
                if (paths is null)
                    throw;
            }
        }

        return BuildSections(paths);
    }

    /// <inheritdoc />
    public async Task<string> GetPageMarkdownAsync(
        string docsRelativePath,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default
    )
    {
        var cacheFile = CacheDir.JoinFile(GetCacheFileName(docsRelativePath));

        if (!forceRefresh && IsFresh(cacheFile))
        {
            try
            {
                return await cacheFile.ReadAllTextAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Failed to read cached markdown for {Path}", docsRelativePath);
            }
        }

        var url = DocumentationConstants.GetRawUrl($"{DocumentationConstants.DocsRoot}/{docsRelativePath}");

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var markdown = await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            try
            {
                CacheDir.Create();
                await cacheFile.WriteAllTextAsync(markdown, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception cacheEx) when (cacheEx is not OperationCanceledException)
            {
                logger.LogWarning(cacheEx, "Failed to write markdown to cache for {Path}", docsRelativePath);
            }

            return markdown;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(
                e,
                "Failed to fetch markdown for {Path}, attempting cached copy",
                docsRelativePath
            );

            if (cacheFile.Exists)
                return await cacheFile.ReadAllTextAsync(cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    private async Task<List<string>> FetchDocsPathsAsync(CancellationToken cancellationToken)
    {
        // Octokit calls don't take a CancellationToken; the tree response is small and
        // the surrounding cache logic still honors cancellation.
        TreeResponse tree;
        try
        {
            tree = await gitHubClient
                .Git.Tree.GetRecursive(
                    DocumentationConstants.Owner,
                    DocumentationConstants.Repo,
                    DocumentationConstants.Branch
                )
                .ConfigureAwait(false);
        }
        catch (NotFoundException e)
        {
            throw new DocumentationNotAvailableException("Documentation repository or branch not found.", e);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var prefix = DocumentationConstants.DocsRoot + "/";

        var paths = tree
            .Tree.Where(item =>
                item.Type == TreeType.Blob && item.Path.StartsWith(prefix, StringComparison.Ordinal)
            )
            .Select(item => item.Path[prefix.Length..])
            .Where(IsDocPage)
            .ToList();

        if (paths.Count == 0)
            throw new DocumentationNotAvailableException(
                "Documentation folder is empty or not available yet."
            );

        return paths;
    }

    /// <summary>
    /// Whether a docs-relative path should be shown as a navigable page.
    /// Excludes images, .gitkeep placeholders, and non-markdown files.
    /// </summary>
    private static bool IsDocPage(string docsRelativePath)
    {
        if (docsRelativePath.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            return false;

        var lastSlash = docsRelativePath.LastIndexOf('/');
        var fileName = lastSlash < 0 ? docsRelativePath : docsRelativePath[(lastSlash + 1)..];
        if (fileName.Equals(".gitkeep", StringComparison.OrdinalIgnoreCase))
            return false;

        return fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Groups discovered docs paths into ordered sections, with the root README first.
    /// </summary>
    internal static IReadOnlyList<DocumentationSection> BuildSections(IReadOnlyList<string> docsRelativePaths)
    {
        var rootPages = new List<DocumentationPage>();
        var sections = new Dictionary<string, List<DocumentationPage>>(StringComparer.OrdinalIgnoreCase);
        var sectionOrder = new List<string>();

        foreach (var path in docsRelativePaths)
        {
            var separatorIndex = path.IndexOf('/');
            if (separatorIndex < 0)
            {
                // Root-level page
                rootPages.Add(
                    new DocumentationPage { Path = path, Title = DocumentationPathResolver.Humanize(path) }
                );
                continue;
            }

            var folder = path[..separatorIndex];
            var fileName = path[(path.LastIndexOf('/') + 1)..];

            if (!sections.TryGetValue(folder, out var list))
            {
                list = [];
                sections[folder] = list;
                sectionOrder.Add(folder);
            }

            list.Add(
                new DocumentationPage { Path = path, Title = DocumentationPathResolver.Humanize(fileName) }
            );
        }

        var result = new List<DocumentationSection>();

        // Root README first, then any other root-level pages, all under an unnamed root section.
        if (rootPages.Count > 0)
        {
            var orderedRoot = rootPages
                .OrderByDescending(p => p.Path.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Add(
                new DocumentationSection
                {
                    Title = string.Empty,
                    FolderName = string.Empty,
                    Pages = orderedRoot,
                }
            );
        }

        foreach (var folder in OrderSections(sectionOrder))
        {
            var pages = OrderSectionPages(sections[folder]);

            result.Add(
                new DocumentationSection
                {
                    Title = DocumentationPathResolver.Humanize(folder),
                    FolderName = folder,
                    Pages = pages,
                }
            );
        }

        return result;
    }

    /// <summary>
    /// Orders section folders by <see cref="DocumentationConstants.PreferredSectionOrder"/>,
    /// with any folder not in that list appended afterwards alphabetically.
    /// </summary>
    private static IEnumerable<string> OrderSections(IEnumerable<string> folders)
    {
        return folders.OrderBy(GetPreferredSectionIndex).ThenBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetPreferredSectionIndex(string folder)
    {
        var order = DocumentationConstants.PreferredSectionOrder;
        for (var i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i], folder, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        // Unknown folders sort after all known ones (then alphabetically via ThenBy).
        return order.Length;
    }

    /// <summary>
    /// Orders pages within a section: <c>overview.md</c> first (matched on file name), then the
    /// remaining pages alphabetically by title.
    /// </summary>
    private static List<DocumentationPage> OrderSectionPages(IEnumerable<DocumentationPage> pages)
    {
        return pages
            .OrderByDescending(p => IsOverviewPage(p.Path))
            .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsOverviewPage(string docsRelativePath)
    {
        var lastSlash = docsRelativePath.LastIndexOf('/');
        var fileName = lastSlash < 0 ? docsRelativePath : docsRelativePath[(lastSlash + 1)..];
        return fileName.Equals("overview.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFresh(FilePath cacheFile) =>
        cacheFile.Exists && DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile) < CacheTtl;

    private static async Task<List<string>?> ReadCachedPathsAsync(FilePath cacheFile, CancellationToken ct)
    {
        try
        {
            if (!cacheFile.Exists)
                return null;

            var text = await cacheFile.ReadAllTextAsync(ct).ConfigureAwait(false);
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Unreadable/corrupt cache — treat as a miss so callers fall back to the network.
            return null;
        }
    }

    private static async Task WriteCachedPathsAsync(
        FilePath cacheFile,
        List<string> paths,
        CancellationToken ct
    )
    {
        CacheDir.Create();
        await cacheFile.WriteAllTextAsync(string.Join('\n', paths), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps a docs-relative path to a safe flat cache file name.
    /// </summary>
    private static string GetCacheFileName(string docsRelativePath)
    {
        var builder = new StringBuilder(docsRelativePath.Length);
        foreach (var c in docsRelativePath)
        {
            builder.Append(c is '/' or '\\' ? '_' : c);
        }

        return "page_" + builder;
    }
}
