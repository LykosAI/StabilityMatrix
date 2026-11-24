using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class GenerationLoraSnapshotTests
{
    private static ComfyNode Loader(object? name) =>
        new()
        {
            ClassType = "LoraLoader",
            Inputs = new Dictionary<string, object?> { ["lora_name"] = name },
        };

    private static string Format(GenerationLoraSnapshot snapshot) =>
        FileNameFormat
            .Parse("{lora_name}", new FileNameFormatProvider { GenerationLoras = snapshot })
            .GetFileName();

    [TestMethod]
    public void NoLoras_UsesFallback()
    {
        Assert.AreEqual("no_lora", Format(GenerationLoraSnapshot.Empty));
        Assert.AreEqual("no_lora", Format(GenerationLoraSnapshot.FromNodes([])));
        Assert.AreEqual(
            "no_lora",
            Format(GenerationLoraSnapshot.FromNodes([Loader(null), Loader(" "), Loader(42)]))
        );
    }

    [TestMethod]
    [DataRow("style.safetensors", "style")]
    [DataRow("folder/style.v2.safetensors", "style.v2")]
    [DataRow("folder\\style.ckpt", "style")]
    [DataRow("C:\\models\\subfolder/style.pt", "style")]
    [DataRow("bad<>:\"|?*\u0001.safetensors", "bad________")]
    [DataRow(" .safetensors", "no_lora")]
    public void SingleLora_StripsFoldersAndExtension_AndSanitizes(string path, string expected)
    {
        Assert.AreEqual(expected, Format(GenerationLoraSnapshot.FromNodes([Loader(path)])));
    }

    [TestMethod]
    public void MultipleLoras_SortsAndDeduplicates_IndependentOfGraphOrder()
    {
        ComfyNode[] nodes = [Loader("z.safetensors"), Loader("a.pt"), Loader("sub/a.pt")];
        var snapshot = GenerationLoraSnapshot.FromNodes(nodes);
        Assert.AreEqual("a_z", Format(snapshot));
        Assert.AreEqual("a_z", Format(GenerationLoraSnapshot.FromNodes(nodes.AsEnumerable().Reverse())));
        CollectionAssert.AreEqual(new[] { "a", "z" }, snapshot.Names.ToArray());
    }

    [TestMethod]
    public void NonLoraNode_WithLoraNamedInput_IsIgnored()
    {
        var node = Loader("unused.safetensors") with { ClassType = "CheckpointLoaderSimple" };
        Assert.AreEqual("no_lora", Format(GenerationLoraSnapshot.FromNodes([node])));
    }

    [TestMethod]
    public void GenerationArgs_CapturesNamesBeforeQueueing_AndRetainsThemAfterGraphEdits()
    {
        var node = Loader("queued.safetensors");
        var nodes = new NodeDictionary { ["Loras_Base"] = node };
        var args = new InferenceGenerationViewModelBase.ImageGenerationEventArgs
        {
            Client = null!,
            Nodes = nodes,
            OutputNodeNames = [],
        };
        node.Inputs["lora_name"] = "changed.safetensors";
        nodes.Clear();
        Assert.AreEqual("queued", Format(args.GenerationLoras));
    }

    [TestMethod]
    public void Sample_ExposesTokenForSettingsPreview_AndSupportsSlicingAndBatchNaming()
    {
        var provider = FileNameFormatProvider.GetSample();
        Assert.IsTrue(provider.TryResolveVariable("lora_name", out var value, out var error), error);
        Assert.AreEqual("sample_lora", value);
        Assert.AreEqual(
            "Grid_sample (1-2)",
            FileNameFormat
                .Parse("{lora_name[:6]}", provider)
                .WithGridPrefix()
                .WithBatchPostFix(1, 2)
                .GetFileName()
        );
        Assert.IsFalse(FileNameFormatProvider.LocalOrganizationVariables.Contains("lora_name"));
    }
}
