using Semver;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Models.Update;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class UpdateViewModelTests
{
    [TestMethod]
    public void FormatChangelogTest()
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

                                ## v2.4.5
                                ### Fixed
                                - Fixed bug

                                ## v2.4.4
                                ### Changed
                                - Changed stuff
                                """;

        // Act
        var result = UpdateViewModel.FormatChangelog(markdown, SemVersion.Parse("2.4.5"));
        var resultPre = UpdateViewModel.FormatChangelog(
            markdown,
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
        var result = UpdateViewModel.FormatChangelog(
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
}
