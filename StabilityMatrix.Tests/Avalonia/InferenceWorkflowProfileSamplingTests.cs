using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class InferenceWorkflowProfileSamplingTests
{
    public static IEnumerable<object[]> ProfilesWithClipType =>
        InferenceWorkflowProfile.All.Where(p => p.ClipType is not null).Select(p => new object[] { p.Key });

    internal static (
        LatentType Latent,
        bool Flux2Scheduler,
        bool FluxGuidance,
        bool AuraFlowShift,
        bool Sd3Shift
    ) SamplingFlags(InferenceWorkflowProfile profile) =>
        (
            profile.Latent,
            profile.UsesFlux2Scheduler,
            profile.UsesFluxGuidance,
            profile.UsesAuraFlowShift,
            profile.UsesSd3ModelSamplingShift
        );

    /// <summary>
    /// Custom with a profile's clip type must sample exactly like that profile. Fails when two
    /// profiles share a clip type but sample differently, which Custom can't tell apart.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(ProfilesWithClipType))]
    public void Custom_WithBuiltInClipType_SamplesLikeTheProfile(string key)
    {
        var profile = InferenceWorkflowProfile.FromKey(key);

        var sampling = InferenceWorkflowProfile.Custom.ForSampling(profile.ClipType);

        Assert.AreEqual(SamplingFlags(profile), SamplingFlags(sampling));
    }

    [TestMethod]
    [DataRow("sd3")]
    [DataRow("chroma")]
    [DataRow("sdxl")]
    [DataRow(null)]
    public void Custom_WithUnownedClipType_SamplesWithCfg(string? clipType)
    {
        var sampling = InferenceWorkflowProfile.Custom.ForSampling(clipType);

        Assert.AreSame(InferenceWorkflowProfile.Custom, sampling);
        Assert.AreEqual(LatentType.Sd3, sampling.Latent);
        Assert.IsFalse(sampling.UsesFluxGuidance);
        Assert.IsFalse(sampling.UsesShift);
    }

    /// <summary>
    /// FluxGuidance drops CFG and the negative prompt, so only models with a guidance embedding
    /// may use it. Of the image models, ComfyUI's official templates use it for Flux.1 alone.
    /// </summary>
    [TestMethod]
    public void FluxGuidance_IsOnlyForFlux()
    {
        CollectionAssert.AreEqual(
            new[] { InferenceWorkflowProfile.Flux },
            InferenceWorkflowProfile.All.Where(p => p.UsesFluxGuidance).ToArray()
        );
    }

    /// <summary>
    /// GGUF UNets sample like any other UNet of their family: Flux keeps FluxGuidance, the rest
    /// keep CFG and the negative prompt, in text-to-image and image-to-image alike.
    /// </summary>
    [TestMethod]
    [DataRow("flux1-dev-Q8_0.gguf", "Flux", true)]
    [DataRow("qwen-image-Q4_K_M.gguf", "QwenImage", false)]
    [DataRow("hidream-i1-full-Q5_K_M.gguf", "HiDream", false)]
    public void GgufUnet_SamplesLikeItsProfile(string fileName, string expectedKey, bool usesFluxGuidance)
    {
        var model = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = fileName,
                SharedFolderType = SharedFolderType.DiffusionModels,
            }
        );

        var profile = InferenceWorkflowProfile.Detect(model, isUnetModel: true);

        Assert.AreEqual(expectedKey, profile.Key);
        Assert.AreEqual(usesFluxGuidance, profile.ForSampling(clipType: null).UsesFluxGuidance);
    }

    [TestMethod]
    public void BuiltInProfile_IgnoresClipType()
    {
        Assert.AreSame(
            InferenceWorkflowProfile.QwenImage,
            InferenceWorkflowProfile.QwenImage.ForSampling("flux2")
        );
    }
}
