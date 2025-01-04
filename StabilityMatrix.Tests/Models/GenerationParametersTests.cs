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
    // basic data
    [DataRow(
        """Steps: 30, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 2216407431, Size: 640x896, Model hash: eb2h052f91, Model: anime_v1""",
        7,
        "30",
        "DPM++ 2M Karras",
        "7",
        "2216407431",
        "640x896",
        "eb2h052f91",
        "anime_v1",
        new string[] { "Steps", "Sampler", "CFG scale", "Seed", "Size", "Model hash", "Model" }
    )]
    // duplicated keys
    [DataRow(
        """Steps: 30, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 2216407431, Size: 640x896, Model hash: eb2h052f91, Model: anime_v1, Steps: 40, Sampler: Whatever, CFG scale: 1, Seed: 1234567890, Size: 1024x1024, Model hash: 1234567890, Model: anime_v2""",
        7,
        "30",
        "DPM++ 2M Karras",
        "7",
        "2216407431",
        "640x896",
        "eb2h052f91",
        "anime_v1",
        new string[] { "Steps", "Sampler", "CFG scale", "Seed", "Size", "Model hash", "Model" }
    )]
    public void TestParseLineFields(
        string line,
        int totalFields,
        string? expectedSteps,
        string? expectedSampler,
        string? expectedCfgScale,
        string? expectedSeed,
        string? expectedSize,
        string? expectedModelHash,
        string? expectedModel,
        string[] expectedKeys
    )
    {
        var fields = GenerationParameters.ParseLine(line);

        Assert.AreEqual(totalFields, fields.Count);
        Assert.AreEqual(expectedSteps, fields["Steps"]);
        Assert.AreEqual(expectedSampler, fields["Sampler"]);
        Assert.AreEqual(expectedCfgScale, fields["CFG scale"]);
        Assert.AreEqual(expectedSeed, fields["Seed"]);
        Assert.AreEqual(expectedSize, fields["Size"]);
        Assert.AreEqual(expectedModelHash, fields["Model hash"]);
        Assert.AreEqual(expectedModel, fields["Model"]);
        CollectionAssert.AreEqual(expectedKeys, fields.Keys);
    }

    [TestMethod]
    // empty line
    [DataRow("", new string[] { })]
    [DataRow("  ", new string[] { })]
    // basic data
    [DataRow(
        "Steps: 30, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 2216407431, Size: 640x896, Model hash: eb2h052f91, Model: anime_v1",
        new string[] { "Steps", "Sampler", "CFG scale", "Seed", "Size", "Model hash", "Model" }
    )]
    // no spaces
    [DataRow(
        "Steps:30,Sampler:DPM++2MKarras,CFGscale:7,Seed:2216407431,Size:640x896,Modelhash:eb2h052f91,Model:anime_v1",
        new string[] { "Steps", "Sampler", "CFGscale", "Seed", "Size", "Modelhash", "Model" }
    )]
    // extra commas
    [DataRow(
        "Steps: 30, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 2216407431, Size: 640x896,,,,,, Model hash: eb2h052f91, Model: anime_v1,,,,,,,",
        new string[] { "Steps", "Sampler", "CFG scale", "Seed", "Size", "Model hash", "Model" }
    )]
    // quoted string
    [DataRow(
        """Name: "John, Doe", Json: {"key:with:colon": "value, with, comma"}, It still: should work""",
        new string[] { "Name", "Json", "It still" }
    )]
    // extra ending brackets
    [DataRow(
        """Name: "John, Doe", Json: {"key:with:colon": "value, with, comma"}}}}}}}})))>>, It still: should work""",
        new string[] { "Name", "Json", "It still" }
    )]
    // civitai
    [DataRow(
        """Steps: 8, Sampler: Euler, CFG scale: 1, Seed: 12346789098, Size: 832x1216, Clip skip: 2, Created Date: 2024-12-22T01:01:01.0222111Z, Civitai resources: [{"type":"checkpoint","modelVersionId":123456,"modelName":"Some model name here [Pony XL] which hopefully doesnt contains half pair of quotes and brackets","modelVersionName":"v2.0"},{"type":"lycoris","weight":0.7,"modelVersionId":11111111,"modelName":"some style","modelVersionName":"v1.0 pony"},{"type":"lora","weight":1,"modelVersionId":222222222,"modelName":"another name","modelVersionName":"v1.0"},{"type":"lora","modelVersionId":3333333,"modelName":"name for 33333333333","modelVersionName":"version name here"}], Civitai metadata: {"remixOfId":11111100000}""",
        new string[]
        {
            "Steps",
            "Sampler",
            "CFG scale",
            "Seed",
            "Size",
            "Clip skip",
            "Created Date",
            "Civitai resources",
            "Civitai metadata"
        }
    )]
    // github.com/nkchocoai/ComfyUI-SaveImageWithMetaData
    [DataRow(
        """Steps: 20, Sampler: DPM++ SDE Karras, CFG scale: 6.0, Seed: 1111111111111, Clip skip: 2, Size: 1024x1024, Model: the_main_model.safetensors, Model hash: ababababab, Lora_0 Model name: name_of_the_first_lora.safetensors, Lora_0 Model hash: ababababab, Lora_0 Strength model: -1.1, Lora_0 Strength clip: -1.1, Lora_1 Model name: name_of_the_second_lora.safetensors, Lora_1 Model hash: ababababab, Lora_1 Strength model: 1, Lora_1 Strength clip: 1, Lora_2 Model name: name_of_the_third_lora.safetensors, Lora_2 Model hash: ababababab, Lora_2 Strength model: 0.9, Lora_2 Strength clip: 0.9, Hashes: {"model": "ababababab", "lora:name_of_the_first_lora": "ababababab", "lora:name_of_the_second_lora": "ababababab", "lora:name_of_the_third_lora": "ababababab"}""",
        new string[]
        {
            "Steps",
            "Sampler",
            "CFG scale",
            "Seed",
            "Clip skip",
            "Size",
            "Model",
            "Model hash",
            "Lora_0 Model name",
            "Lora_0 Model hash",
            "Lora_0 Strength model",
            "Lora_0 Strength clip",
            "Lora_1 Model name",
            "Lora_1 Model hash",
            "Lora_1 Strength model",
            "Lora_1 Strength clip",
            "Lora_2 Model name",
            "Lora_2 Model hash",
            "Lora_2 Strength model",
            "Lora_2 Strength clip",
            "Hashes"
        }
    )]
    // asymmetrical bracket
    [DataRow(
        """Steps: 20, Missing closing bracket: {"name": "Someone did not close [this bracket"}, But: the parser, should: still return, the: fields before it""",
        new string[] { "Steps", "Missing closing bracket" }
    )]
    public void TestParseLineEdgeCases(string line, string[] expectedKeys)
    {
        var fields = GenerationParameters.ParseLine(line);

        Assert.AreEqual(expectedKeys.Length, fields.Count);
        CollectionAssert.AreEqual(expectedKeys, fields.Keys);
    }

    [TestMethod]
    public void TestParseLine()
    {
        var fields = GenerationParameters.ParseLine(
            """Steps: 8, Sampler: Euler, CFG scale: 1, Seed: 12346789098, Size: 832x1216, Clip skip: 2, """
                + """Created Date: 2024-12-22T01:01:01.0222111Z, Civitai resources: [{"type":"checkpoint","modelVersionId":123456,"modelName":"Some model name here [Pony XL] which hopefully doesnt contains half pair of quotes and brackets","modelVersionName":"v2.0"},{"type":"lycoris","weight":0.7,"modelVersionId":11111111,"modelName":"some style","modelVersionName":"v1.0 pony"},{"type":"lora","weight":1,"modelVersionId":222222222,"modelName":"another name","modelVersionName":"v1.0"},{"type":"lora","modelVersionId":3333333,"modelName":"name for 33333333333","modelVersionName":"version name here"}], Civitai metadata: {"remixOfId":11111100000},"""
                + """Hashes: {"model": "1234455678", "lora:aaaaaaa": "1234455678", "lora:bbbbbb": "1234455678", "lora:cccccccc": "1234455678"}"""
        );

        Assert.AreEqual(10, fields.Count);
        Assert.AreEqual("8", fields["Steps"]);
        Assert.AreEqual("Euler", fields["Sampler"]);
        Assert.AreEqual("1", fields["CFG scale"]);
        Assert.AreEqual("12346789098", fields["Seed"]);
        Assert.AreEqual("832x1216", fields["Size"]);
        Assert.AreEqual("2", fields["Clip skip"]);
        Assert.AreEqual("2024-12-22T01:01:01.0222111Z", fields["Created Date"]);
        Assert.AreEqual(
            """[{"type":"checkpoint","modelVersionId":123456,"modelName":"Some model name here [Pony XL] which hopefully doesnt contains half pair of quotes and brackets","modelVersionName":"v2.0"},{"type":"lycoris","weight":0.7,"modelVersionId":11111111,"modelName":"some style","modelVersionName":"v1.0 pony"},{"type":"lora","weight":1,"modelVersionId":222222222,"modelName":"another name","modelVersionName":"v1.0"},{"type":"lora","modelVersionId":3333333,"modelName":"name for 33333333333","modelVersionName":"version name here"}]""",
            fields["Civitai resources"]
        );
        Assert.AreEqual("""{"remixOfId":11111100000}""", fields["Civitai metadata"]);
        Assert.AreEqual(
            """{"model": "1234455678", "lora:aaaaaaa": "1234455678", "lora:bbbbbb": "1234455678", "lora:cccccccc": "1234455678"}""",
            fields["Hashes"]
        );
    }
}
