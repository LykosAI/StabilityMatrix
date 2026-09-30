using System.Globalization;
using System.Reflection;
using NSubstitute;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Tokens;
using StabilityMatrix.Core.Services;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class PromptTests
{
    private ITokenizerProvider tokenizerProvider = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        tokenizerProvider = Substitute.For<ITokenizerProvider>();

        var promptSyntaxFile = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("StabilityMatrix.Tests.ImagePrompt.tmLanguage.json")!;

        var registry = new Registry(new RegistryOptions(ThemeName.DarkPlus));
        var grammar = registry.LoadGrammarFromStream(promptSyntaxFile);

        tokenizerProvider.TokenizeLine(Arg.Any<string>()).Returns(x => grammar.TokenizeLine(x.Arg<string>()));
    }

    [TestMethod]
    public void TestPromptProcessedText()
    {
        var prompt = Prompt.FromRawText("test", tokenizerProvider);

        prompt.Process();

        Assert.AreEqual("test", prompt.ProcessedText);
    }

    [TestMethod]
    public void TestPromptWeightParsing()
    {
        var prompt = Prompt.FromRawText("<lora:my_model:1.5>", tokenizerProvider);

        prompt.Process();

        // Output should have no loras
        Assert.AreEqual("", prompt.ProcessedText);

        var network = prompt.ExtraNetworks[0];

        Assert.AreEqual(PromptExtraNetworkType.Lora, network.Type);
        Assert.AreEqual("my_model", network.Name);
        Assert.AreEqual(1.5f, network.ModelWeight);
    }

    /// <summary>
    /// Tests that we can parse decimal numbers with different cultures
    /// </summary>
    [TestMethod]
    public void TestPromptWeightParsing_DecimalSeparatorCultures_ShouldParse()
    {
        var prompt = Prompt.FromRawText("<lora:my_model:1.5>", tokenizerProvider);

        // Cultures like de-DE use commas as decimal separators, check that we can parse those too
        ExecuteWithCulture(() => prompt.Process(), CultureInfo.GetCultureInfo("de-DE"));

        // Output should have no loras
        Assert.AreEqual("", prompt.ProcessedText);

        var network = prompt.ExtraNetworks![0];

        Assert.AreEqual(PromptExtraNetworkType.Lora, network.Type);
        Assert.AreEqual("my_model", network.Name);
        Assert.AreEqual(1.5f, network.ModelWeight);
    }

    [TestMethod]
    public void PromptLoras_ReachOutputFilename_UsingResolvedLocalFiles()
    {
        var prompt = Prompt.FromRawText("cat <lora:style:0.8> <lora:character:1>", tokenizerProvider);
        prompt.Process();
        var index = Substitute.For<IModelIndexService>();
        index.ModelIndex.Returns(
            new Dictionary<SharedFolderType, List<LocalModelFile>>
            {
                [SharedFolderType.Lora] =
                [
                    new LocalModelFile
                    {
                        RelativePath = "Lora/sub/style.safetensors",
                        SharedFolderType = SharedFolderType.Lora,
                    },
                    new LocalModelFile
                    {
                        RelativePath = "Lora/character.safetensors",
                        SharedFolderType = SharedFolderType.Lora,
                    },
                ],
            }
        );
        var builder = new ComfyNodeBuilder();
        builder.Group_LoraLoadMany(
            "Prompt",
            new ModelNodeConnection { Data = ["Checkpoint", 0] },
            new ClipNodeConnection { Data = ["Checkpoint", 1] },
            prompt.GetExtraNetworksAsLocalModels(index)
        );
        var provider = new FileNameFormatProvider
        {
            GenerationLoras = GenerationLoraSnapshot.FromNodes(builder.Nodes.Values),
        };
        Assert.AreEqual("cat  ", prompt.ProcessedText);
        Assert.AreEqual("character_style", FileNameFormat.Parse("{lora_name}", provider).GetFileName());
    }

    private static T? ExecuteWithCulture<T>(Func<T> func, CultureInfo culture)
    {
        var result = default(T);

        var thread = new Thread(() =>
        {
            result = func();
        })
        {
            CurrentCulture = culture,
        };

        thread.Start();
        thread.Join();

        return result;
    }

    private static void ExecuteWithCulture(Action func, CultureInfo culture)
    {
        var thread = new Thread(() =>
        {
            func();
        })
        {
            CurrentCulture = culture,
        };

        thread.Start();
        thread.Join();
    }
}
