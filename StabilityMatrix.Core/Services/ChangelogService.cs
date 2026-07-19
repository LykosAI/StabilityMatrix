using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Semver;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Updater;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IChangelogService, ChangelogService>]
public partial class ChangelogService : IChangelogService
{
    private readonly ILogger<ChangelogService> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ISettingsManager settingsManager;
    private readonly DebugOptions debugOptions;

    /// <inheritdoc />
    public string GitHubChangelogUrl => "https://github.com/LykosAI/StabilityMatrix/blob/main/CHANGELOG.md";

    private string UpdateManifestUrl =>
        debugOptions.UpdateManifestUrl ?? UpdateHelper.DefaultUpdateManifestUrl;

    private static DirectoryPath CacheDirectory => Compat.AppDataHome.JoinDir("Cache");

    public ChangelogService(
        ILogger<ChangelogService> logger,
        IHttpClientFactory httpClientFactory,
        ISettingsManager settingsManager,
        IOptions<DebugOptions> debugOptions
    )
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.settingsManager = settingsManager;
        this.debugOptions = debugOptions.Value;
    }

    [GeneratedRegex(
        @"(##\s*(v[0-9]+\.[0-9]+\.[0-9]+(?:-(?:[0-9A-Za-z-.]+))?)(.+?))(?=(##\s*v[0-9]+\.[0-9]+\.[0-9]+)|\z)",
        RegexOptions.Singleline
    )]
    private static partial Regex RegexChangelog();

    /// <inheritdoc />
    public async Task<string> GetReleaseNotes(
        string changelogUrl,
        CancellationToken cancellationToken = default
    )
    {
        using var client = httpClientFactory.CreateClient();

        try
        {
            using var response = await client.GetAsync(changelogUrl, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var changelog = await response
                    .Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Formatting for new changelog format
                // https://keepachangelog.com/en/1.1.0/
                if (changelogUrl.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    return FormatChangelog(
                            changelog,
                            Compat.AppVersion,
                            settingsManager.Settings.PreferredUpdateChannel
                        ) ?? "## Unable to format release notes";
                }

                return changelog;
            }

            return "## Unable to load release notes";
        }
        catch (HttpRequestException e)
        {
            return $"## Unable to fetch release notes ({e.StatusCode})\n\n[{changelogUrl}]({changelogUrl})";
        }
        catch (TaskCanceledException) { }

        return $"## Unable to fetch release notes\n\n[{changelogUrl}]({changelogUrl})";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChangelogEntry>> GetChangelogEntries(
        CancellationToken cancellationToken = default
    )
    {
        var channel = settingsManager.Settings.PreferredUpdateChannel;

        // Try to fetch fresh markdown from the changelog url for the user's channel
        if (await FetchChangelogMarkdown(channel, cancellationToken).ConfigureAwait(false) is { } markdown)
        {
            // Cache the successful fetch for offline fallback
            await TryWriteCache(channel, markdown, cancellationToken).ConfigureAwait(false);
            return ParseEntries(markdown, channel);
        }

        // Fall back to the last successfully cached markdown
        if (await TryReadCache(channel, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            logger.LogInformation("Using cached changelog for channel {Channel}", channel);
            return ParseEntries(cached, channel);
        }

        // Neither network nor cache available
        return [];
    }

    /// <summary>
    /// Fetch the raw changelog markdown for the given channel, deriving the url from the
    /// update manifest (same source the updater uses). Returns null on any failure.
    /// </summary>
    private async Task<string?> FetchChangelogMarkdown(
        UpdateChannel channel,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var changelogUrl = await GetChangelogUrlForChannel(channel, cancellationToken)
                .ConfigureAwait(false);
            if (changelogUrl is null)
            {
                logger.LogWarning("No changelog url found in manifest for channel {Channel}", channel);
                return null;
            }

            using var client = httpClientFactory.CreateClient("UpdateClient");
            using var response = await client.GetAsync(changelogUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Changelog fetch returned {StatusCode} for {Url}",
                    response.StatusCode,
                    changelogUrl
                );
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
            when (e is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            logger.LogWarning(e, "Failed to fetch changelog for channel {Channel}", channel);
            return null;
        }
    }

    /// <summary>
    /// Resolve the changelog url for the given channel from the update manifest, using the same
    /// channel-inclusion order the updater uses (highest applicable channel first).
    /// </summary>
    private async Task<string?> GetChangelogUrlForChannel(
        UpdateChannel channel,
        CancellationToken cancellationToken
    )
    {
        var client = httpClientFactory.CreateClient("UpdateClient");
        using var response = await client
            .GetAsync(UpdateManifestUrl, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Update manifest fetch returned {StatusCode}", response.StatusCode);
            return null;
        }

        var manifest = await JsonSerializer
            .DeserializeAsync<UpdateManifest>(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (manifest is null)
        {
            logger.LogError("UpdateManifest is null");
            return null;
        }

        // Prefer the highest channel at or below the requested channel that has a platform build
        foreach (
            var candidate in Enum.GetValues<UpdateChannel>()
                .Where(c => c > UpdateChannel.Unknown && c <= channel)
                .OrderByDescending(c => c)
        )
        {
            if (
                manifest.Updates.TryGetValue(candidate, out var platforms)
                && platforms.GetInfoForCurrentPlatform() is { } info
            )
            {
                return info.Changelog.ToString();
            }
        }

        return null;
    }

    private static async Task<string?> TryReadCache(
        UpdateChannel channel,
        CancellationToken cancellationToken
    )
    {
        var cacheFile = GetCacheFile(channel);
        if (!cacheFile.Exists)
            return null;

        try
        {
            return await cacheFile.ReadAllTextAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private async Task TryWriteCache(
        UpdateChannel channel,
        string markdown,
        CancellationToken cancellationToken
    )
    {
        try
        {
            CacheDirectory.Create();
            await GetCacheFile(channel).WriteAllTextAsync(markdown, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            logger.LogWarning(e, "Failed to write changelog cache for channel {Channel}", channel);
        }
    }

    private static FilePath GetCacheFile(UpdateChannel channel) =>
        CacheDirectory.JoinFile($"changelog-{channel.ToString().ToLowerInvariant()}.md");

    /// <summary>
    /// Parses all version blocks from changelog markdown, newest first, filtered to the given
    /// channel (using the same channel-inclusion logic as <see cref="FormatChangelog"/>).
    /// Includes the current version and older.
    /// </summary>
    internal static IReadOnlyList<ChangelogEntry> ParseEntries(
        string markdown,
        UpdateChannel maxChannel = UpdateChannel.Stable
    )
    {
        return RegexChangelog()
            .Matches(markdown)
            .Select(m => new
            {
                Block = m.Groups[1].Value.Trim(),
                Version = SemVersion.TryParse(
                    m.Groups[2].Value.Trim(),
                    SemVersionStyles.AllowV,
                    out var version
                )
                    ? version
                    : null,
            })
            .Where(x => x.Version is not null)
            .Where(x => IsChannelIncluded(x.Version!, maxChannel))
            .Select(x => new ChangelogEntry { Version = x.Version!, Markdown = x.Block })
            .ToList();
    }

    /// <summary>
    /// Whether a version's pre-release identifier is included for the given max channel.
    /// Matches the filtering used by <see cref="FormatChangelog"/>.
    /// </summary>
    private static bool IsChannelIncluded(SemVersion version, UpdateChannel maxChannel)
    {
        return version.PrereleaseIdentifiers.Count == 0
            || version.PrereleaseIdentifiers[0].Value switch
            {
                "pre" when maxChannel >= UpdateChannel.Preview => true,
                "dev" when maxChannel >= UpdateChannel.Development => true,
                _ => false,
            };
    }

    /// <summary>
    /// Formats changelog markdown including up to the current version
    /// </summary>
    /// <param name="markdown">Markdown to format</param>
    /// <param name="currentVersion">Versions equal or below this are excluded</param>
    /// <param name="maxChannel">Maximum channel level to include</param>
    internal static string? FormatChangelog(
        string markdown,
        SemVersion currentVersion,
        UpdateChannel maxChannel = UpdateChannel.Stable
    )
    {
        var pattern = RegexChangelog();

        var results = pattern
            .Matches(markdown)
            .Select(m => new
            {
                Block = m.Groups[1].Value.Trim(),
                Version = SemVersion.TryParse(
                    m.Groups[2].Value.Trim(),
                    SemVersionStyles.AllowV,
                    out var version
                )
                    ? version
                    : null,
                Content = m.Groups[3].Value.Trim(),
            })
            .Where(x => x.Version is not null)
            .ToList();

        // Join all blocks until and excluding the current version
        // If we're on a pre-release, include the current release
        var currentVersionBlock = results.FindIndex(x => x.Version == currentVersion.WithoutMetadata());

        // For mismatching build metadata, add one
        if (
            currentVersionBlock != -1
            && results[currentVersionBlock].Version?.Metadata != currentVersion.Metadata
        )
        {
            currentVersionBlock++;
        }

        // Support for previous pre-release without changelogs
        if (currentVersionBlock == -1)
        {
            currentVersionBlock = results.FindIndex(x =>
                x.Version == currentVersion.WithoutPrereleaseOrMetadata()
            );

            // Add 1 if found to include the current release
            if (currentVersionBlock != -1)
            {
                currentVersionBlock++;
            }
        }

        // Still not found, just include all
        if (currentVersionBlock == -1)
        {
            currentVersionBlock = results.Count;
        }

        // Filter out pre-releases
        var blocks = results
            .Take(currentVersionBlock)
            .Where(x =>
                x.Version!.PrereleaseIdentifiers.Count == 0
                || x.Version.PrereleaseIdentifiers[0].Value switch
                {
                    "pre" when maxChannel >= UpdateChannel.Preview => true,
                    "dev" when maxChannel >= UpdateChannel.Development => true,
                    _ => false,
                }
            )
            .Select(x => x.Block);

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }
}
