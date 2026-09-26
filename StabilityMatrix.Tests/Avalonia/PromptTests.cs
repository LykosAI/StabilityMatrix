using System.Globalization;
using System.Reflection;
using NSubstitute;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Exceptions;
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

    [TestMethod]
    public void GetDiagnostics_UnknownModels_ReportsEachWithItsSpan()
    {
        const string text = "<lora:missing_a:1>, cat, <lora:missing_b:0.5>";
        var prompt = Prompt.FromRawText(text, tokenizerProvider);

        var diagnostics = prompt.GetDiagnostics(CreateModelIndex());

        Assert.AreEqual(2, diagnostics.Count);
        CollectionAssert.AllItemsAreInstancesOfType(diagnostics.ToList(), typeof(PromptUnknownModelError));
        Assert.AreEqual("missing_a", text[diagnostics[0].TextOffset..diagnostics[0].TextEndOffset]);
        Assert.AreEqual("missing_b", text[diagnostics[1].TextOffset..diagnostics[1].TextEndOffset]);
    }

    [TestMethod]
    public void GetDiagnostics_KnownModel_ReportsNothing()
    {
        var prompt = Prompt.FromRawText("<lora:my_model:1.5>, cat", tokenizerProvider);

        var diagnostics = prompt.GetDiagnostics(CreateModelIndex("Lora/my_model.safetensors"));

        Assert.AreEqual(0, diagnostics.Count);
    }

    [TestMethod]
    public void GetDiagnostics_WithoutModelIndex_SkipsModelChecks()
    {
        var prompt = Prompt.FromRawText("<lora:missing:1>", tokenizerProvider);

        Assert.AreEqual(0, prompt.GetDiagnostics().Count);
    }

    [TestMethod]
    public void GetDiagnostics_MismatchedParenthesis_ReportsReadableMessage()
    {
        var prompt = Prompt.FromRawText("cat)", tokenizerProvider);

        var diagnostics = prompt.GetDiagnostics();

        Assert.AreEqual(1, diagnostics.Count);
        Assert.AreEqual("Mismatched closing parenthesis ')'", diagnostics[0].Message);
    }

    [TestMethod]
    public void GetDiagnostics_DoesNotAddToModelIndex()
    {
        var modelIndex = CreateModelIndex();
        var prompt = Prompt.FromRawText("<lora:missing:1>", tokenizerProvider);

        prompt.GetDiagnostics(modelIndex);

        Assert.AreEqual(0, modelIndex.ModelIndex.Count);
    }

    [TestMethod]
    public void ValidateExtraNetworks_UnknownModel_StillThrows()
    {
        var prompt = Prompt.FromRawText("<lora:missing:1>", tokenizerProvider);

        Assert.ThrowsException<PromptUnknownModelError>(() => prompt.ValidateExtraNetworks(CreateModelIndex())
        );
    }

    private static IModelIndexService CreateModelIndex(params string[] loraRelativePaths)
    {
        var index = new Dictionary<SharedFolderType, List<LocalModelFile>>();
        if (loraRelativePaths.Length > 0)
        {
            index[SharedFolderType.Lora] = loraRelativePaths
                .Select(path => new LocalModelFile
                {
                    RelativePath = path,
                    SharedFolderType = SharedFolderType.Lora,
                })
                .ToList();
        }

        var service = Substitute.For<IModelIndexService>();
        service.ModelIndex.Returns(index);
        return service;
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
