using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class MossImageProfileTests
{
    private static HybridModelFile UnetModel(string fileName, string? baseModel = null) =>
        HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = fileName,
                SharedFolderType = SharedFolderType.DiffusionModels,
                ConnectedModelInfo = baseModel is null
                    ? null
                    : new ConnectedModelInfo { BaseModel = baseModel },
            }
        );

    [TestMethod]
    [DataRow("moss_image_sft_int8_convrot.safetensors")]
    [DataRow("moss_image_base_bf16.safetensors")]
    [DataRow("MOSS-Image-SFT.safetensors")]
    public void Detect_MossFileNames(string fileName)
    {
        Assert.AreSame(
            InferenceWorkflowProfile.MossImage,
            InferenceWorkflowProfile.Detect(UnetModel(fileName), true)
        );
    }

    [TestMethod]
    public void Detect_CivitaiBaseModelTag()
    {
        var finetune = UnetModel("my-anime-finetune-v2.safetensors", baseModel: "MOSS Image");

        Assert.AreSame(InferenceWorkflowProfile.MossImage, InferenceWorkflowProfile.Detect(finetune, true));
    }

    [TestMethod]
    [DataRow("qwen3.5_9b_w4a8_gptq.safetensors")]
    [DataRow("qwen3.5_9b_int8_convrot.safetensors")]
    [DataRow("qwen3.5_9b_bf16.safetensors")]
    [DataRow("qwen3_5_9b.safetensors")]
    public void EncoderCompanion_AcceptsQwen35_9bBuilds(string fileName)
    {
        var companion = InferenceWorkflowProfile.MossImage.GetEncoderCompanions(null).Single();

        Assert.IsTrue(companion.MatchesFileName(fileName));
    }

    /// <summary>Other Qwen encoders are shape-incompatible with MOSS and must not auto-fill.</summary>
    [TestMethod]
    [DataRow("qwen_3_8b.safetensors")]
    [DataRow("qwen_3_4b.safetensors")]
    [DataRow("qwen3vl_4b_fp8_scaled.safetensors")]
    [DataRow("qwen_2.5_vl_7b_fp8_scaled.safetensors")]
    [DataRow("qwen3.5_4b.safetensors")]
    public void EncoderCompanion_RejectsOtherQwenEncoders(string fileName)
    {
        var companion = InferenceWorkflowProfile.MossImage.GetEncoderCompanions(null).Single();

        Assert.IsFalse(companion.MatchesFileName(fileName));
    }

    [TestMethod]
    public void OtherProfiles_DoNotClaimTheMossEncoder()
    {
        const string mossEncoder = "qwen3.5_9b_w4a8_gptq.safetensors";

        foreach (
            var profile in InferenceWorkflowProfile.All.Where(p => p != InferenceWorkflowProfile.MossImage)
        )
        {
            foreach (var companion in profile.GetEncoderCompanions(null))
            {
                Assert.IsFalse(
                    companion.MatchesFileName(mossEncoder),
                    $"{profile.Key}: '{companion.DisplayName}' matches the MOSS encoder"
                );
            }
        }
    }

    [TestMethod]
    public void SkipLayerGuidanceNode_UsesComfyInputNames()
    {
        var nodes = new NodeDictionary();
        nodes.AddTypedNode(
            new ComfyNodeBuilder.SkipLayerGuidanceDiTSimple
            {
                Name = "slg",
                Model = new ModelNodeConnection { Data = ["unet", 0] },
                DoubleLayers = "",
                SingleLayers = "22",
                StartPercent = 0,
                EndPercent = 0.5,
            }
        );
        var node = nodes["slg"];

        Assert.AreEqual("SkipLayerGuidanceDiTSimple", node.ClassType);
        CollectionAssert.AreEquivalent(
            new[] { "model", "double_layers", "single_layers", "start_percent", "end_percent" },
            node.Inputs.Keys.ToArray()
        );
        Assert.AreEqual("22", node.Inputs["single_layers"]);
    }
}
