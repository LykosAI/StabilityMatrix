using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StabilityMatrix.Core.Models.Documentation;

/// <summary>
/// Pure helpers for the documentation viewer: humanizing file/folder names and
/// resolving links and image paths encountered inside rendered markdown.
/// Kept free of Avalonia/IO dependencies so it can be unit tested in isolation.
/// </summary>
public static class DocumentationPathResolver
{
    /// <summary>
    /// The classification of a link clicked within a rendered markdown page.
    /// </summary>
    public enum LinkKind
    {
        /// <summary>Relative link to another markdown page inside the docs (navigate in-app).</summary>
        InternalPage,

        /// <summary>Absolute http(s) link (open in the system browser).</summary>
        External,

        /// <summary>In-page anchor (e.g. <c>#section</c>) or otherwise unhandled — ignore.</summary>
        Anchor,
    }

    /// <summary>
    /// Result of classifying/resolving a clicked link.
    /// </summary>
    /// <param name="Kind">The classification of the link.</param>
    /// <param name="Target">
    /// For <see cref="LinkKind.InternalPage"/>, the resolved docs-root-relative page path.
    /// For <see cref="LinkKind.External"/>, the absolute URL.
    /// For <see cref="LinkKind.Anchor"/>, the bare heading slug (no leading <c>#</c>).
    /// </param>
    /// <param name="Fragment">
    /// For an <see cref="LinkKind.InternalPage"/> that carried a <c>#fragment</c>, the bare
    /// heading slug to scroll to after the page loads; otherwise <c>null</c>.
    /// </param>
    public readonly record struct ResolvedLink(LinkKind Kind, string Target, string? Fragment = null);

    /// <summary>
    /// Produces a GitHub-style anchor slug from heading text: lowercased, with everything
    /// except letters, digits, spaces and hyphens removed, and spaces collapsed to hyphens.
    /// </summary>
    /// <remarks>
    /// Duplicate-heading disambiguation (the <c>-1</c>, <c>-2</c> suffixes GitHub adds) is applied
    /// by the caller in document order, not here.
    /// </remarks>
    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == '-')
                builder.Append(c);
            else if (char.IsWhiteSpace(c))
                builder.Append(' ');
            // all other characters (punctuation, parentheses, etc.) are dropped
        }

        // Collapse runs of whitespace to single hyphens
        var words = builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('-', words);
    }

    /// <summary>
    /// Converts a docs file or folder name into a human friendly title.
    /// Strips a trailing <c>.md</c>, replaces kebab/snake separators with spaces, and title-cases.
    /// <c>README</c> maps to "Home".
    /// </summary>
    public static string Humanize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Drop a trailing .md extension if present
        if (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            name = name[..^3];

        if (name.Equals("README", StringComparison.OrdinalIgnoreCase))
            return "Home";

        var words = name.Replace('-', ' ')
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var builder = new StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                builder.Append(' ');
            builder.Append(TitleCaseWord(words[i]));
        }

        return builder.ToString();
    }

    private static string TitleCaseWord(string word)
    {
        // Preserve all-caps acronyms (e.g. "GPU", "AMD", "CUDA")
        if (word.Length > 1 && word.All(char.IsUpper))
            return word;

        return char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..].ToLowerInvariant();
    }

    /// <summary>
    /// Classifies a clicked link relative to the currently displayed page.
    /// </summary>
    /// <param name="currentPagePath">
    /// Path of the current page relative to the docs root (e.g. <c>advanced/environment-variables.md</c>).
    /// </param>
    /// <param name="href">The raw href from the markdown link.</param>
    /// <returns>
    /// For <see cref="LinkKind.InternalPage"/>, <c>Target</c> is the resolved docs-root-relative path.
    /// For <see cref="LinkKind.External"/>, <c>Target</c> is the absolute URL.
    /// For <see cref="LinkKind.Anchor"/>, <c>Target</c> is the original href.
    /// </returns>
    public static ResolvedLink ResolveLink(string currentPagePath, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return new ResolvedLink(LinkKind.Anchor, href ?? string.Empty);

        href = href.Trim();

        // In-page anchor -> carry the bare heading slug as the target
        if (href.StartsWith('#'))
            return new ResolvedLink(LinkKind.Anchor, Slugify(href.TrimStart('#')));

        // Absolute http(s) (also covers mailto: etc. -> treat as external / open-in-browser)
        if (
            Uri.TryCreate(href, UriKind.Absolute, out var abs)
            && (
                abs.Scheme == Uri.UriSchemeHttp
                || abs.Scheme == Uri.UriSchemeHttps
                || abs.Scheme == Uri.UriSchemeMailto
            )
        )
        {
            return new ResolvedLink(LinkKind.External, href);
        }

        // Split off any anchor fragment from a relative path before resolving the page
        string? fragment = null;
        var fragmentIndex = href.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            var rawFragment = href[(fragmentIndex + 1)..];
            if (!string.IsNullOrWhiteSpace(rawFragment))
                fragment = Slugify(rawFragment);
            href = href[..fragmentIndex];
        }

        // A bare "#" (or "path#" with no page part) was really an in-page anchor
        if (string.IsNullOrEmpty(href))
            return new ResolvedLink(LinkKind.Anchor, fragment ?? string.Empty);

        // Relative markdown page -> resolve against the current page's folder, carrying any fragment
        if (href.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ResolveRelativePath(currentPagePath, href);
            return new ResolvedLink(LinkKind.InternalPage, resolved, fragment);
        }

        // Anything else relative (rare) -> treat as external best-effort against raw base
        return new ResolvedLink(
            LinkKind.External,
            DocumentationConstants.GetRawUrl(CombineDocsPath(ResolveRelativePath(currentPagePath, href)))
        );
    }

    // Matches markdown image syntax ![alt](src "optional title")
    private static readonly Regex ImageRegex = new(
        @"!\[(?<alt>[^\]]*)\]\((?<src>[^)\s]+)(?<rest>[^)]*)\)",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Rewrites relative image sources in a markdown document into absolute raw URLs so they
    /// render inside the viewer. Absolute URLs are left untouched.
    /// </summary>
    /// <param name="currentPagePath">Docs-root-relative path of the current page.</param>
    /// <param name="markdown">The raw markdown content.</param>
    public static string RewriteImageUrls(string currentPagePath, string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown;

        return ImageRegex.Replace(
            markdown,
            match =>
            {
                var alt = match.Groups["alt"].Value;
                var src = match.Groups["src"].Value;
                var rest = match.Groups["rest"].Value;
                var resolved = ResolveImageUrl(currentPagePath, src);
                return $"![{alt}]({resolved}{rest})";
            }
        );
    }

    /// <summary>
    /// Resolves a relative image path in the current page into an absolute raw URL so it renders.
    /// </summary>
    /// <param name="currentPagePath">Docs-root-relative path of the current page.</param>
    /// <param name="src">The raw image src from the markdown.</param>
    public static string ResolveImageUrl(string currentPagePath, string src)
    {
        if (string.IsNullOrWhiteSpace(src))
            return src;

        src = src.Trim();

        // Already absolute -> leave as-is
        if (Uri.TryCreate(src, UriKind.Absolute, out _))
            return src;

        var resolvedDocsRelative = ResolveRelativePath(currentPagePath, src);
        return DocumentationConstants.GetRawUrl(CombineDocsPath(resolvedDocsRelative));
    }

    /// <summary>
    /// Resolves a relative path (e.g. <c>../images/foo.png</c>) against the folder of the
    /// current page and returns a normalized docs-root-relative path (using forward slashes,
    /// with any leading <c>./</c> and traversal <c>..</c> segments collapsed).
    /// </summary>
    public static string ResolveRelativePath(string currentPagePath, string relative)
    {
        relative = relative.Replace('\\', '/');

        // Absolute-from-docs-root (leading slash) -> relative to docs root
        if (relative.StartsWith('/'))
            return NormalizeSegments(relative.TrimStart('/').Split('/'));

        var currentDir = GetDirectory(currentPagePath);
        var combined = string.IsNullOrEmpty(currentDir) ? relative : currentDir + "/" + relative;
        return NormalizeSegments(combined.Split('/'));
    }

    private static string NormalizeSegments(string[] segments)
    {
        var stack = new System.Collections.Generic.List<string>();
        foreach (var segment in segments)
        {
            if (segment is "" or ".")
                continue;

            if (segment == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(segment);
        }

        return string.Join('/', stack);
    }

    private static string GetDirectory(string path)
    {
        path = path.Replace('\\', '/');
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    /// <summary>
    /// Prefixes a docs-root-relative path with the repository docs root folder.
    /// </summary>
    private static string CombineDocsPath(string docsRelativePath) =>
        $"{DocumentationConstants.DocsRoot}/{docsRelativePath.TrimStart('/')}";
}
