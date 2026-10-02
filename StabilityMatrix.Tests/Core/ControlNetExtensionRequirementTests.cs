using Semver;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class ControlNetExtensionRequirementTests
{
    [TestMethod]
    [DataRow("v0.2.0", false)]
    [DataRow("v0.4.2", false)]
    [DataRow("v0.5.2", true)]
    [DataRow("v0.5.3", true)]
    public void PreprocessorWorkflow_RequiresPublishedCompatibilityFixes(string installedTag, bool satisfies)
    {
        var nodes = new NodeDictionary();
        nodes.AddTypedNode(
            new ComfyNodeBuilder.AIOPreprocessor
            {
                Name = "ControlNet_Preprocessor",
                Image = new ImageNodeConnection { Data = ["LoadImage", 0] },
                Preprocessor = "CannyEdgePreprocessor",
            }
        );

        var specifier = nodes.RequiredExtensions.Single();
        var installed = new InstalledPackageExtension
        {
            Paths = [new DirectoryPath(Path.GetTempPath(), "ComfyUI-Inference-Core-Nodes")],
            GitRepositoryUrl = "https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes.git",
            Version = new PackageExtensionVersion { Tag = installedTag },
        };

        var matched = ExtensionMatcher.FindInstalled(specifier, [installed]);
        Assert.IsNotNull(
            matched,
            "An older version must be recognized as installed before checking its tag."
        );
        Assert.IsTrue(specifier.TryGetSemVersionRange(out var range));
        Assert.IsTrue(SemVersion.TryParse(matched.Version!.Tag, SemVersionStyles.AllowV, out var version));
        Assert.AreEqual(satisfies, range!.Contains(version!));
    }
}
