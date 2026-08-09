using System.Text.Json;
using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class InferenceWorkflowProfileSerializationTests
{
    [TestMethod]
    public void Serializes_AsStableStringKey()
    {
        Assert.AreEqual("\"Flux2\"", JsonSerializer.Serialize(InferenceWorkflowProfile.Flux2));
        Assert.AreEqual("\"QwenImage\"", JsonSerializer.Serialize(InferenceWorkflowProfile.QwenImage));
    }

    [TestMethod]
    public void Reads_StringKeys_CaseInsensitive()
    {
        Assert.AreEqual(
            InferenceWorkflowProfile.Krea2Turbo,
            JsonSerializer.Deserialize<InferenceWorkflowProfile>("\"krea2turbo\"")
        );
    }

    /// <summary>
    /// Pre-2.17 saved projects persisted the numeric enum value — those must keep loading.
    /// </summary>
    [TestMethod]
    public void Reads_LegacyNumericValues()
    {
        Assert.AreEqual(
            InferenceWorkflowProfile.Custom,
            JsonSerializer.Deserialize<InferenceWorkflowProfile>("8")
        );
        Assert.AreEqual(
            InferenceWorkflowProfile.ZImageTurbo,
            JsonSerializer.Deserialize<InferenceWorkflowProfile>("5")
        );
    }

    /// <summary>
    /// A project saved by a newer app version may carry a profile this build doesn't know —
    /// it must load as Auto rather than throw.
    /// </summary>
    [TestMethod]
    public void UnknownValues_FallBackToAuto()
    {
        Assert.AreEqual(
            InferenceWorkflowProfile.Auto,
            JsonSerializer.Deserialize<InferenceWorkflowProfile>("\"SomeFutureModel\"")
        );
        Assert.AreEqual(
            InferenceWorkflowProfile.Auto,
            JsonSerializer.Deserialize<InferenceWorkflowProfile>("999")
        );
    }

    [TestMethod]
    public void RoundTrips_EveryProfile()
    {
        foreach (var profile in Enum.GetValues<InferenceWorkflowProfile>())
        {
            var json = JsonSerializer.Serialize(profile);
            Assert.AreEqual(profile, JsonSerializer.Deserialize<InferenceWorkflowProfile>(json));
        }
    }
}
