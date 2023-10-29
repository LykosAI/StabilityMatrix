using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Tests.Models;

[TestClass]
public class GenerationParametersTests
{
    [TestMethod]
    public void TestParse()
    {
        const string data = """
                            test123
                            Negative prompt: test, easy negative
                            Steps: 20, Sampler: Euler a, CFG scale: 7, Seed: 3589107295, Size: 1024x1028, Model hash: 9aa0c3e54d, Model: nightvisionXL_v0770_BakedVAE, VAE hash: 235745af8d, VAE: sdxl_vae.safetensors, Style Selector Enabled: True, Style Selector Randomize: False, Style Selector Style: base, Version: 1.6.0
                            """;

        Assert.IsTrue(GenerationParameters.TryParse(data, out var result));

        Assert.AreEqual("test123", result.PositivePrompt);
        Assert.AreEqual("test, easy negative", result.NegativePrompt);
        Assert.AreEqual(20, result.Steps);
        Assert.AreEqual("Euler a", result.Sampler);
        Assert.AreEqual(7, result.CfgScale);
        Assert.AreEqual(3589107295, result.Seed);
        Assert.AreEqual(1024, result.Width);
        Assert.AreEqual(1028, result.Height);
        Assert.AreEqual("9aa0c3e54d", result.ModelHash);
        Assert.AreEqual("nightvisionXL_v0770_BakedVAE", result.ModelName);
    }

    [TestMethod]
    public void TestParse_NoNegative()
    {
        const string data = """
                                test123
                                Steps: 20, Sampler: Euler a, CFG scale: 7, Seed: 3589107295, Size: 1024x1028, Model hash: 9aa0c3e54d, Model: nightvisionXL_v0770_BakedVAE, VAE hash: 235745af8d, VAE: sdxl_vae.safetensors, Style Selector Enabled: True, Style Selector Randomize: False, Style Selector Style: base, Version: 1.6.0
                                """;

        Assert.IsTrue(GenerationParameters.TryParse(data, out var result));

        Assert.AreEqual("test123", result.PositivePrompt);
        Assert.IsNull(result.NegativePrompt);
        Assert.AreEqual(20, result.Steps);
        Assert.AreEqual("Euler a", result.Sampler);
        Assert.AreEqual(7, result.CfgScale);
        Assert.AreEqual(3589107295, result.Seed);
        Assert.AreEqual(1024, result.Width);
        Assert.AreEqual(1028, result.Height);
        Assert.AreEqual("9aa0c3e54d", result.ModelHash);
        Assert.AreEqual("nightvisionXL_v0770_BakedVAE", result.ModelName);
    }

    [TestMethod]
    public void TestParseLineFields()
    {
        const string lastLine =
            @"Steps: 30, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 2216407431, Size: 640x896, Model hash: eb2h052f91, Model: anime_v1";

        var fields = GenerationParameters.ParseLine(lastLine);

        Assert.AreEqual(7, fields.Count);
        Assert.AreEqual("30", fields["Steps"]);
        Assert.AreEqual("DPM++ 2M Karras", fields["Sampler"]);
        Assert.AreEqual("7", fields["CFG scale"]);
        Assert.AreEqual("2216407431", fields["Seed"]);
        Assert.AreEqual("640x896", fields["Size"]);
        Assert.AreEqual("eb2h052f91", fields["Model hash"]);
        Assert.AreEqual("anime_v1", fields["Model"]);
    }
}
