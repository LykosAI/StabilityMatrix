using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class DocumentationServiceBuildSectionsTests
{
    private static readonly List<string> SamplePaths =
    [
        "README.md",
        "changelog.md",
        "advanced/environment-variables.md",
        "advanced/overview.md",
        "getting-started/installation.md",
        "getting-started/overview.md",
        "inference/overview.md",
        "troubleshooting/faq.md",
        "zzz-extra/page.md",
    ];

    [TestMethod]
    public void BuildSections_OrdersSectionsByPreferredOrder_UnknownLast()
    {
        var sections = DocumentationService.BuildSections(SamplePaths);

        // Root (empty-title) section is first, followed by folders in preferred order,
        // then unknown folders alphabetically.
        var folderNames = sections.Select(s => s.FolderName).ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                string.Empty, // root
                "getting-started",
                "inference",
                "advanced",
                "troubleshooting",
                "zzz-extra", // unknown -> last
            },
            folderNames
        );
    }

    [TestMethod]
    public void BuildSections_OverviewIsFirstWithinSection()
    {
        var sections = DocumentationService.BuildSections(SamplePaths);

        var advanced = sections.Single(s => s.FolderName == "advanced");
        Assert.AreEqual("advanced/overview.md", advanced.Pages[0].Path);
        Assert.AreEqual("advanced/environment-variables.md", advanced.Pages[1].Path);

        var gettingStarted = sections.Single(s => s.FolderName == "getting-started");
        Assert.AreEqual("getting-started/overview.md", gettingStarted.Pages[0].Path);
        Assert.AreEqual("getting-started/installation.md", gettingStarted.Pages[1].Path);
    }

    [TestMethod]
    public void BuildSections_RootReadmeIsFirst()
    {
        var sections = DocumentationService.BuildSections(SamplePaths);

        var root = sections.First();
        Assert.AreEqual(string.Empty, root.FolderName);
        Assert.AreEqual("README.md", root.Pages[0].Path);
        // Remaining root pages are alphabetical by title.
        Assert.AreEqual("changelog.md", root.Pages[1].Path);
    }

    [TestMethod]
    public void BuildSections_UnknownFoldersSortedAlphabeticallyAfterKnown()
    {
        var paths = new List<string>
        {
            "zebra/overview.md",
            "alpha/overview.md",
            "getting-started/overview.md",
        };

        var sections = DocumentationService.BuildSections(paths);

        CollectionAssert.AreEqual(
            new[] { "getting-started", "alpha", "zebra" },
            sections.Select(s => s.FolderName).ToArray()
        );
    }
}
