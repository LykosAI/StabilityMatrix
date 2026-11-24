using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class EncoderCollisionReviewTests
{
    private const string H3 = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors";
    private const string Krea = "qwen3vl_4b_fp8_scaled.safetensors";

    [TestMethod]
    public void KreaMatcherMustRejectH3Encoder()
    {
        var companion = InferenceWorkflowProfile.Krea2Turbo.GetEncoderCompanions(null)[0];
        Assert.IsTrue(companion.MatchesFileName(Krea));
        Assert.IsFalse(companion.MatchesFileName(H3), "Krea accepts MiniMax's 32B encoder");
    }

    [TestMethod]
    public void H3FallbackMustRejectKreaEncoder()
    {
        Assert.IsTrue(VideoWorkflowProfile.MiniMaxH3.TextEncoderAutoFill[0](H3));
        Assert.IsFalse(
            VideoWorkflowProfile.MiniMaxH3.TextEncoderAutoFill.Any(m => m(Krea)),
            "H3 fallback accepts Krea's 4B encoder"
        );
    }

    [TestMethod]
    public void KreaWithBothInstalledMustSelect4B()
    {
        var companion = InferenceWorkflowProfile.Krea2Turbo.GetEncoderCompanions(null)[0];
        var installed = new[] { H3, Krea }.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(Krea, installed.FirstOrDefault(companion.MatchesFileName));
    }
}
