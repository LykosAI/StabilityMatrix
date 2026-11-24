using StabilityMatrix.Core.Api.PromptGenApi;

namespace StabilityMatrix.Tests.Core;

/// <summary>
/// Pins the PromptGen model configuration names the desktop client sends. The server looks
/// these up as case-sensitive dictionary keys, so a drift here surfaces as a 500 in the field.
/// </summary>
[TestClass]
public class PromptAmplifierModelTests
{
    [DataTestMethod]
    [DataRow(PromptAmplifierModel.SparkClassic, false, "PromptV1Dev")]
    [DataRow(PromptAmplifierModel.SparkClassic, true, "PromptV1ThinkingDev")]
    [DataRow(PromptAmplifierModel.SparkV2, false, "PromptV2Dev")]
    [DataRow(PromptAmplifierModel.SparkV2, true, "PromptV2ThinkingDev")]
    public void ToPromptGenModelName_MapsEveryFamilyAndThinkingCombination(
        PromptAmplifierModel model,
        bool isThinkingEnabled,
        string expected
    )
    {
        Assert.AreEqual(expected, model.ToPromptGenModelName(isThinkingEnabled));
    }

    [TestMethod]
    public void PersistedValues_AreStable()
    {
        Assert.AreEqual(0, (int)PromptAmplifierModel.SparkClassic);
        Assert.AreEqual(1, (int)PromptAmplifierModel.SparkV2);
    }
}
