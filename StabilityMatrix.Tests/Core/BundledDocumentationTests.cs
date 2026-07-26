using StabilityMatrix.Core.Models.Documentation;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class BundledDocumentationTests
{
    /// <summary>
    /// A broken embed glob or LogicalName still compiles and just yields an empty bundle,
    /// which would silently remove the offline fallback.
    /// </summary>
    [TestMethod]
    public void Paths_ContainsDocsTree()
    {
        var paths = BundledDocumentation.Paths;

        Assert.IsTrue(paths.Count > 0, "No docs were embedded — check the EmbeddedResource glob.");
        CollectionAssert.Contains(paths.ToList(), "README.md");
        Assert.IsTrue(
            paths.Any(p => p.StartsWith("getting-started/", StringComparison.Ordinal)),
            "Nested docs should keep forward-slash relative paths, got: " + string.Join(", ", paths.Take(5))
        );
    }

    /// <summary>
    /// The VitePress site build pulls in dependency READMEs several times the size of the
    /// docs themselves; they must not ride along into the shipped assembly.
    /// </summary>
    [TestMethod]
    public void Paths_ExcludesNodeModulesAndSiteBuild()
    {
        foreach (var path in BundledDocumentation.Paths)
        {
            Assert.IsFalse(path.Contains("node_modules", StringComparison.OrdinalIgnoreCase), path);
            Assert.IsFalse(path.Contains(".vitepress", StringComparison.OrdinalIgnoreCase), path);
        }
    }

    [TestMethod]
    public void TryReadPage_ReturnsContentForBundledPage()
    {
        var markdown = BundledDocumentation.TryReadPage("README.md");

        Assert.IsNotNull(markdown);
        Assert.IsTrue(markdown.Length > 0);
    }

    [TestMethod]
    public void TryReadPage_ReturnsNullForUnknownPage()
    {
        Assert.IsNull(BundledDocumentation.TryReadPage("nope/not-a-real-page.md"));
    }
}
