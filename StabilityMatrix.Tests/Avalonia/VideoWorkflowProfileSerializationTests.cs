using System.Text.Json;
using StabilityMatrix.Avalonia.Models.Inference;

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
}
