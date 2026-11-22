using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class NodeDictionaryRequiredExtensionsTests
{
    private static ComfyNodeBuilder.PromptExpansion PromptExpansion() =>
        new()
        {
            Name = "PromptExpansion",
            ModelName = "model",
            Text = "prompt",
            Seed = 0,
        };

    private static ComfyNodeBuilder.LayeredDiffusionDecodeRgba LayerDiffuseDecode() =>
        new()
        {
            Name = "LayerDiffuseDecode",
            Samples = new LatentNodeConnection { Data = ["Sampler", 0] },
            Images = new ImageNodeConnection { Data = ["VAEDecode", 0] },
            SdVersion = "SDXL",
        };

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SameExtension_ReturnsHighestMinimumVersion(bool decodeFirst)
    {
        var nodes = new NodeDictionary();
        if (decodeFirst)
        {
            nodes.AddTypedNode(LayerDiffuseDecode());
            nodes.AddTypedNode(PromptExpansion());
        }
        else
        {
            nodes.AddTypedNode(PromptExpansion());
            nodes.AddTypedNode(LayerDiffuseDecode());
        }

        var specifier = nodes.RequiredExtensions.Single();

        Assert.AreEqual("https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes", specifier.Name);
        Assert.AreEqual(">=", specifier.Constraint);
        Assert.AreEqual("0.5.3", specifier.Version);
    }
}
