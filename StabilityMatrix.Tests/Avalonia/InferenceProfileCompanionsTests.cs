using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class InferenceProfileCompanionsTests
{
    private static IEnumerable<InferenceWorkflowProfile> StandaloneProfiles =>
        InferenceWorkflowProfile.All.Where(p => p.UsesStandaloneLoader is true);

    [TestMethod]
    public void StandaloneProfiles_HaveEncoderAndVaeCompanions()
    {
        foreach (var profile in StandaloneProfiles)
        {
            var encoders = profile.GetEncoderCompanions(null);
            Assert.IsTrue(encoders.Count > 0, $"{profile.Key} should have at least one encoder companion");
            Assert.IsNotNull(profile.VaeCompanion, $"{profile.Key} should have a VAE companion");
        }
    }

    /// <summary>
    /// The matcher and the download resource of a companion must agree: downloading the
    /// official file has to produce a filename the matcher recognizes, or the offer would
    /// re-appear forever after a successful download. Catches URL/matcher drift.
    /// </summary>
    [TestMethod]
    public void CompanionMatchers_AcceptTheirOwnResourceFileNames()
    {
        foreach (var profile in StandaloneProfiles)
        {
            var companions = profile.GetEncoderCompanions(null).ToList();

            if (profile.VaeCompanion is { } vae)
            {
                companions.Add(vae);
            }

            foreach (var companion in companions)
            {
                var fileName = companion.Resource.FileName;
                Assert.IsTrue(
                    companion.MatchesFileName(fileName),
                    $"{profile.Key}: matcher for '{companion.DisplayName}' rejects its own download '{fileName}'"
                );
            }
        }
    }

    [TestMethod]
    public void NonStandaloneProfiles_HaveNoCompanions()
    {
        InferenceWorkflowProfile[] profiles =
        [
            InferenceWorkflowProfile.Auto,
            InferenceWorkflowProfile.DefaultCheckpoint,
            InferenceWorkflowProfile.Custom,
        ];

        foreach (var profile in profiles)
        {
            Assert.AreEqual(
                0,
                profile.GetEncoderCompanions(null).Count,
                $"{profile.Key} should have no encoder companions"
            );
            Assert.IsNull(profile.VaeCompanion, $"{profile.Key} should have no VAE companion");
        }
    }

    /// <summary>
    /// A profile's encoder companions must never cross-match another family's encoder — e.g.
    /// Krea 2's qwen3vl_4b file must not satisfy Qwen Image (2.5-VL 7B) or Klein (text-only
    /// qwen_3_4b), which are tensor-shape-incompatible at sampling time.
    /// </summary>
    [TestMethod]
    public void QwenFamilyEncoderMatchers_DoNotCrossMatch()
    {
        const string kleinEncoder = "qwen_3_4b.safetensors";
        const string kreaEncoder = "qwen3vl_4b_fp8_scaled.safetensors";
        const string qwenImageEncoder = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
        const string animaEncoder = "qwen_3_06b_base.safetensors";

        var qwenImage = InferenceWorkflowProfile.QwenImage.GetEncoderCompanions(null).Single();
        var krea = InferenceWorkflowProfile.Krea2Turbo.GetEncoderCompanions(null).Single();
        var flux2 = InferenceWorkflowProfile.Flux2.GetEncoderCompanions(null).Single();
        var anima = InferenceWorkflowProfile.Anima.GetEncoderCompanions(null).Single();

        Assert.IsFalse(qwenImage.MatchesFileName(kreaEncoder));
        Assert.IsFalse(qwenImage.MatchesFileName(kleinEncoder));
        Assert.IsFalse(qwenImage.MatchesFileName(animaEncoder));

        Assert.IsFalse(krea.MatchesFileName(qwenImageEncoder));
        Assert.IsFalse(krea.MatchesFileName(kleinEncoder));
        Assert.IsFalse(krea.MatchesFileName(animaEncoder));

        Assert.IsFalse(flux2.MatchesFileName(kreaEncoder));
        Assert.IsFalse(flux2.MatchesFileName(qwenImageEncoder));
        Assert.IsFalse(flux2.MatchesFileName(animaEncoder));

        Assert.IsFalse(anima.MatchesFileName(kreaEncoder));
        Assert.IsFalse(anima.MatchesFileName(qwenImageEncoder));
        Assert.IsFalse(anima.MatchesFileName(kleinEncoder));
    }

    /// <summary>
    /// Legacy numeric values and string keys must both be unique — they are the persisted
    /// identities of saved projects.
    /// </summary>
    [TestMethod]
    public void ProfileIdentities_AreUnique()
    {
        var all = InferenceWorkflowProfile.All;

        CollectionAssert.AllItemsAreUnique(all.Select(p => p.Key).ToList());
        CollectionAssert.AllItemsAreUnique(
            all.Where(p => p.LegacyValue is not null).Select(p => p.LegacyValue).ToList()
        );
        Assert.AreSame(
            InferenceWorkflowProfile.Custom,
            all[^1],
            "Custom must stay last in the dropdown order"
        );
    }
}
