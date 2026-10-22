using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models;

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
    [DataRow("wan")]
    [DataRow(null)]
    public void Custom_WithUnownedClipType_KeepsSd3LatentAndFluxGuidance(string? clipType)
    {
        var sampling = InferenceWorkflowProfile.Custom.ForSampling(clipType);

        Assert.AreSame(InferenceWorkflowProfile.Custom, sampling);
        Assert.AreEqual(LatentType.Sd3, sampling.Latent);
        Assert.IsTrue(sampling.UsesFluxGuidance);
        Assert.IsFalse(sampling.UsesShift);
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
