using Microsoft.VisualStudio.TestTools.UnitTesting;
using StabilityMatrix.Core.Models.Documentation;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class DocumentationPathResolverTests
{
    [DataTestMethod]
    [DataRow("overview.md", "Overview")]
    [DataRow("environment-variables.md", "Environment Variables")]
    [DataRow("first-launch", "First Launch")]
    [DataRow("getting-started", "Getting Started")]
    [DataRow("data_directory", "Data Directory")]
    [DataRow("README.md", "Home")]
    [DataRow("README", "Home")]
    public void Humanize_ProducesTitleCase(string input, string expected)
    {
        Assert.AreEqual(expected, DocumentationPathResolver.Humanize(input));
    }

    [TestMethod]
    public void Humanize_PreservesAcronyms()
    {
        Assert.AreEqual("GPU Backends", DocumentationPathResolver.Humanize("GPU-backends"));
    }

    [TestMethod]
    public void ResolveLink_SiblingMarkdown_ResolvesRelativeToCurrentFolder()
    {
        var result = DocumentationPathResolver.ResolveLink(
            "advanced/environment-variables.md",
            "overview.md"
        );

        Assert.AreEqual(DocumentationPathResolver.LinkKind.InternalPage, result.Kind);
        Assert.AreEqual("advanced/overview.md", result.Target);
    }

    [TestMethod]
    public void ResolveLink_ParentMarkdown_ResolvesTraversal()
    {
        var result = DocumentationPathResolver.ResolveLink(
            "advanced/environment-variables.md",
            "../README.md"
        );

        Assert.AreEqual(DocumentationPathResolver.LinkKind.InternalPage, result.Kind);
        Assert.AreEqual("README.md", result.Target);
    }

    [TestMethod]
    public void ResolveLink_MarkdownFromRoot_ResolvesIntoSubfolder()
    {
        var result = DocumentationPathResolver.ResolveLink("README.md", "getting-started/overview.md");

        Assert.AreEqual(DocumentationPathResolver.LinkKind.InternalPage, result.Kind);
        Assert.AreEqual("getting-started/overview.md", result.Target);
    }

    [TestMethod]
    public void ResolveLink_MarkdownWithFragment_StripsFragment()
    {
        var result = DocumentationPathResolver.ResolveLink(
            "advanced/environment-variables.md",
            "overview.md#setting-variables"
        );

        Assert.AreEqual(DocumentationPathResolver.LinkKind.InternalPage, result.Kind);
        Assert.AreEqual("advanced/overview.md", result.Target);
    }

    [TestMethod]
    public void ResolveLink_External_IsClassifiedExternal()
    {
        var result = DocumentationPathResolver.ResolveLink(
            "advanced/environment-variables.md",
            "https://docs.pytorch.org/docs/stable/torch_environment_variables.html"
        );

        Assert.AreEqual(DocumentationPathResolver.LinkKind.External, result.Kind);
        Assert.AreEqual(
            "https://docs.pytorch.org/docs/stable/torch_environment_variables.html",
            result.Target
        );
    }

    [TestMethod]
    public void ResolveLink_Anchor_IsClassifiedAnchor()
    {
        var result = DocumentationPathResolver.ResolveLink(
            "advanced/environment-variables.md",
            "#common-variables"
        );

        Assert.AreEqual(DocumentationPathResolver.LinkKind.Anchor, result.Kind);
        // Target is the bare slug (no leading '#').
        Assert.AreEqual("common-variables", result.Target);
    }

    [TestMethod]
    public void ResolveLink_AnchorWithMixedCaseAndPunctuation_SlugifiesTarget()
    {
        var result = DocumentationPathResolver.ResolveLink("advanced/overview.md", "#Apple Silicon (MPS)");

        Assert.AreEqual(DocumentationPathResolver.LinkKind.Anchor, result.Kind);
        Assert.AreEqual("apple-silicon-mps", result.Target);
    }

    [TestMethod]
    public void ResolveLink_MarkdownWithFragment_CarriesSlugFragment()
    {
        var result = DocumentationPathResolver.ResolveLink(
            "advanced/environment-variables.md",
            "overview.md#Setting Variables"
        );

        Assert.AreEqual(DocumentationPathResolver.LinkKind.InternalPage, result.Kind);
        Assert.AreEqual("advanced/overview.md", result.Target);
        Assert.AreEqual("setting-variables", result.Fragment);
    }

    [TestMethod]
    public void ResolveLink_MarkdownWithoutFragment_HasNullFragment()
    {
        var result = DocumentationPathResolver.ResolveLink("README.md", "getting-started/overview.md");

        Assert.AreEqual(DocumentationPathResolver.LinkKind.InternalPage, result.Kind);
        Assert.IsNull(result.Fragment);
    }

    [DataTestMethod]
    [DataRow("AMD (ROCm)", "amd-rocm")]
    [DataRow("Apple Silicon (MPS)", "apple-silicon-mps")]
    [DataRow("Common Variables", "common-variables")]
    [DataRow("Already-Slugged", "already-slugged")]
    [DataRow("  Trailing / Slashes!  ", "trailing-slashes")]
    [DataRow("Multiple   Spaces", "multiple-spaces")]
    public void Slugify_ProducesGitHubStyleSlug(string input, string expected)
    {
        Assert.AreEqual(expected, DocumentationPathResolver.Slugify(input));
    }

    [TestMethod]
    public void ResolveImageUrl_RelativeParentPath_ResolvesToRawUrl()
    {
        var result = DocumentationPathResolver.ResolveImageUrl(
            "advanced/environment-variables.md",
            "../images/advanced/envar-window.png"
        );

        Assert.AreEqual(
            "https://raw.githubusercontent.com/LykosAI/StabilityMatrix/main/docs/images/advanced/envar-window.png",
            result
        );
    }

    [TestMethod]
    public void ResolveImageUrl_AbsoluteUrl_IsUnchanged()
    {
        const string absolute = "https://example.com/foo.png";
        var result = DocumentationPathResolver.ResolveImageUrl("advanced/overview.md", absolute);

        Assert.AreEqual(absolute, result);
    }

    [TestMethod]
    public void RewriteImageUrls_RewritesOnlyRelativeImages()
    {
        const string markdown =
            "Text\n\n![editor](../images/advanced/envar-window.png)\n\n![remote](https://example.com/x.png)\n";

        var result = DocumentationPathResolver.RewriteImageUrls(
            "advanced/environment-variables.md",
            markdown
        );

        StringAssert.Contains(
            result,
            "![editor](https://raw.githubusercontent.com/LykosAI/StabilityMatrix/main/docs/images/advanced/envar-window.png)"
        );
        // Absolute image left untouched
        StringAssert.Contains(result, "![remote](https://example.com/x.png)");
    }

    [TestMethod]
    public void RewriteImageUrls_PreservesImageTitle()
    {
        const string markdown = "![alt](img/pic.png \"A title\")";

        var result = DocumentationPathResolver.RewriteImageUrls("getting-started/overview.md", markdown);

        StringAssert.Contains(
            result,
            "https://raw.githubusercontent.com/LykosAI/StabilityMatrix/main/docs/getting-started/img/pic.png \"A title\""
        );
    }
}
