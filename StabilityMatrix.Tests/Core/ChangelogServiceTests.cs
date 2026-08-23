using Semver;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ChangelogServiceTests
{
    private const string SampleMarkdown = """
        # Changelog

        All notable changes to Stability Matrix will be documented in this file.

        The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
        and this project adheres to [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

        ## v2.4.6
        ### Added
        - Stuff
        ### Changed
        - Things

        ## v2.4.5
        ### Fixed
        - Fixed bug

        ## v2.4.4
        ### Changed
        - Changed stuff
        """;

    [TestMethod]
    public void FormatChangelogTest()
    {
        // Act
        var result = ChangelogService.FormatChangelog(SampleMarkdown, SemVersion.Parse("2.4.5"));
        var resultPre = ChangelogService.FormatChangelog(
            SampleMarkdown,
            SemVersion.Parse("2.4.5-pre.1+1a7b4e4")
        );

        // Assert
        const string expected = """
            ## v2.4.6
            ### Added
            - Stuff
            ### Changed
            - Things
            """;

        Assert.AreEqual(expected, result);

        // Pre-release should include the current release
        const string expectedPre = """
            ## v2.4.6
            ### Added
            - Stuff
            ### Changed
            - Things

            ## v2.4.5
            ### Fixed
            - Fixed bug
            """;
        Assert.AreEqual(expectedPre, resultPre);
    }

    [TestMethod]
    public void FormatChangelogWithChannelTest()
    {
        // Arrange
        const string markdown = """
            # Changelog

            All notable changes to Stability Matrix will be documented in this file.

            The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
            and this project adheres to [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

            ## v2.4.6
            ### Added
            - Stuff
            ### Changed
            - Things

            ## v2.4.6-pre.1
            ### Fixed
            - Fixed bug

            ## v2.4.6-dev.1
            ### Fixed
            - Fixed bug

            ## v2.4.5
            ### Changed
            - Changed stuff
            """;

        // Act
        var result = ChangelogService.FormatChangelog(
            markdown,
            SemVersion.Parse("2.4.0"),
            UpdateChannel.Preview
        );

        // Assert
        const string expected = """
            ## v2.4.6
            ### Added
            - Stuff
            ### Changed
            - Things

            ## v2.4.6-pre.1
            ### Fixed
            - Fixed bug

            ## v2.4.5
            ### Changed
            - Changed stuff
            """;

        // Should include pre but not dev
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ParseEntriesReturnsAllBlocksNewestFirst()
    {
        // Act - Stable channel excludes pre-releases
        var entries = ChangelogService.ParseEntries(SampleMarkdown);

        // Assert - three stable versions, newest first, including the current version and older
        Assert.AreEqual(3, entries.Count);
        Assert.AreEqual(SemVersion.Parse("2.4.6"), entries[0].Version);
        Assert.AreEqual(SemVersion.Parse("2.4.5"), entries[1].Version);
        Assert.AreEqual(SemVersion.Parse("2.4.4"), entries[2].Version);

        // Each entry markdown includes its version heading
        StringAssert.StartsWith(entries[0].Markdown, "## v2.4.6");
        StringAssert.Contains(entries[0].Markdown, "- Stuff");
    }

    [TestMethod]
    public void ParseEntriesFiltersByChannel()
    {
        // Arrange
        const string markdown = """
            # Changelog

            ## v2.4.6
            ### Added
            - Stuff

            ## v2.4.6-pre.1
            ### Fixed
            - Pre bug

            ## v2.4.6-dev.1
            ### Fixed
            - Dev bug

            ## v2.4.5
            ### Changed
            - Changed stuff
            """;

        // Act
        var stable = ChangelogService.ParseEntries(markdown);
        var preview = ChangelogService.ParseEntries(markdown, UpdateChannel.Preview);
        var dev = ChangelogService.ParseEntries(markdown, UpdateChannel.Development);

        // Assert
        // Stable: only non-prerelease versions
        CollectionAssert.AreEqual(
            new[] { SemVersion.Parse("2.4.6"), SemVersion.Parse("2.4.5") },
            stable.Select(e => e.Version).ToArray()
        );

        // Preview: stable + pre, but not dev
        CollectionAssert.AreEqual(
            new[] { SemVersion.Parse("2.4.6"), SemVersion.Parse("2.4.6-pre.1"), SemVersion.Parse("2.4.5") },
            preview.Select(e => e.Version).ToArray()
        );

        // Development: everything
        CollectionAssert.AreEqual(
            new[]
            {
                SemVersion.Parse("2.4.6"),
                SemVersion.Parse("2.4.6-pre.1"),
                SemVersion.Parse("2.4.6-dev.1"),
                SemVersion.Parse("2.4.5"),
            },
            dev.Select(e => e.Version).ToArray()
        );
    }
}
