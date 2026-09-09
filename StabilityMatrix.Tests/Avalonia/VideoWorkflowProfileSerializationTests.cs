using System.Text.Json;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class VideoWorkflowProfileSerializationTests
{
    [TestMethod]
    public void Serializes_AsStableStringKey()
    {
        Assert.AreEqual("\"Wan\"", JsonSerializer.Serialize(VideoWorkflowProfile.Wan));
        Assert.AreEqual("\"Auto\"", JsonSerializer.Serialize(VideoWorkflowProfile.Auto));
    }

    [TestMethod]
    public void Reads_StringKeys_CaseInsensitive()
    {
        Assert.AreEqual(
            VideoWorkflowProfile.Wan,
            JsonSerializer.Deserialize<VideoWorkflowProfile>("\"wan\"")
        );
    }

    /// <summary>
    /// Pre-profile saved projects carry no profile property at all; a missing value simply
    /// leaves the card's Auto default in place. A project saved by a newer app version may
    /// carry a profile this build doesn't know — it must load as Auto rather than throw.
    /// </summary>
    [TestMethod]
    public void UnknownValues_FallBackToAuto()
    {
        Assert.AreEqual(
            VideoWorkflowProfile.Auto,
            JsonSerializer.Deserialize<VideoWorkflowProfile>("\"SomeFutureModel\"")
        );
        Assert.AreEqual(VideoWorkflowProfile.Auto, JsonSerializer.Deserialize<VideoWorkflowProfile>("999"));
    }

    [TestMethod]
    public void RoundTrips_EveryProfile()
    {
        foreach (var profile in VideoWorkflowProfile.All)
        {
            var json = JsonSerializer.Serialize(profile);
            Assert.AreSame(profile, JsonSerializer.Deserialize<VideoWorkflowProfile>(json));
        }
    }

    [TestMethod]
    public void Detect_NeverReturnsAuto()
    {
        Assert.AreNotEqual(VideoWorkflowProfile.Auto, VideoWorkflowProfile.Detect(null));
    }

    [TestMethod]
    [DataRow("minimax_h3_fl2va_pruned_int8_convrot.safetensors", "MiniMaxH3")]
    [DataRow("MiniMax-H3-fl2va-Q3_K_L.gguf", "MiniMaxH3")]
    [DataRow("wan2.2_ti2v_5B_fp16.safetensors", "Wan")]
    [DataRow("wan2.2_t2v_high_noise_14B_fp8_scaled.safetensors", "Wan")]
    [DataRow("some_unknown_video_model.safetensors", "Wan")]
    public void Detect_ByFileName(string fileName, string expectedKey)
    {
        var model = HybridModelFile.FromRemote($"diffusion_models/{fileName}");
        Assert.AreEqual(expectedKey, VideoWorkflowProfile.Detect(model).Key);
    }

    /// <summary>MiniMax H3 frame counts must sit on the 17k+5 grid at 24 fps, rounding up.</summary>
    [TestMethod]
    [DataRow(1, 5)]
    [DataRow(5, 5)]
    [DataRow(6, 22)]
    [DataRow(73, 73)]
    [DataRow(120, 124)]
    [DataRow(124, 124)]
    public void MiniMaxH3_SnapFrames(int frames, int expected)
    {
        Assert.AreEqual(expected, VideoWorkflowProfile.MiniMaxH3.SnapFrames(frames));
    }

    [TestMethod]
    public void SnapFrames_IsIdentity_WithoutFrameGrid()
    {
        Assert.AreEqual(33, VideoWorkflowProfile.Wan.SnapFrames(33));
        Assert.AreEqual(34, VideoWorkflowProfile.Wan.SnapFrames(34));
    }

    /// <summary>Auto-fill matchers tolerate separator/case differences in community filenames.</summary>
    [TestMethod]
    public void AutoFillMatchers_MatchOfficialAndRenamedFiles()
    {
        var wan21Unet = HybridModelFile.FromRemote("wan2.1_t2v_14B_fp8.safetensors");
        var wan225bUnet = HybridModelFile.FromRemote("wan2.2_ti2v_5B_fp16.safetensors");

        var wan21Vae = VideoWorkflowProfile.Wan.VaeAutoFill(wan21Unet)!;
        Assert.IsTrue(wan21Vae("wan_2.1_vae.safetensors"));
        Assert.IsTrue(wan21Vae("Wan2.1-VAE.safetensors"));
        Assert.IsFalse(wan21Vae("wan2.2_vae.safetensors"));

        var wan22Vae = VideoWorkflowProfile.Wan.VaeAutoFill(wan225bUnet)!;
        Assert.IsTrue(wan22Vae("wan2.2_vae.safetensors"));
        Assert.IsFalse(wan22Vae("wan_2.1_vae.safetensors"));

        Assert.IsTrue(
            VideoWorkflowProfile.Wan.TextEncoderAutoFill[0]("umt5_xxl_fp8_e4m3fn_scaled.safetensors")
        );

        var h3 = VideoWorkflowProfile.MiniMaxH3;
        Assert.IsTrue(h3.VaeAutoFill(null)!("minimax_h3_video_vae_fp16.safetensors"));
        Assert.IsFalse(h3.VaeAutoFill(null)!("minimax_h3_audio_vae_fp32.safetensors"));
        Assert.IsTrue(h3.AudioVaeAutoFill!("minimax_h3_audio_vae_fp32.safetensors"));

        // Only the MiniMax-specific 32B encoder is eligible for automatic selection.
        Assert.IsTrue(h3.TextEncoderAutoFill[0]("qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"));
        Assert.IsFalse(h3.TextEncoderAutoFill[0]("Qwen3-VL-8B-Instruct-fp8.safetensors"));
        Assert.IsFalse(h3.TextEncoderAutoFill.Any(m => m("Qwen3-VL-8B-Instruct-fp8.safetensors")));
        Assert.IsFalse(h3.TextEncoderAutoFill.Any(m => m("qwen_2.5_vl_7b_fp8_scaled.safetensors")));
        Assert.IsFalse(h3.TextEncoderAutoFill.Any(m => m("qwen_3_4b.safetensors")));
    }
}
