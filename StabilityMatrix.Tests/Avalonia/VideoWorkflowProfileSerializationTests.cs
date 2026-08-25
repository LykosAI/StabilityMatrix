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
}
