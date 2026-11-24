using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Inference;
using Size = System.Drawing.Size;

namespace StabilityMatrix.Tests.Avalonia;

/// <summary>
/// Builds the real Text to Image and Image to Image view models for each workflow profile
/// and checks which ComfyUI node types end up in the prompt graph. The expected lists are
/// snapshots: a change to them should be a deliberate change to how a profile samples.
/// </summary>
[TestClass]
public class InferenceWorkflowGraphTests
{
    private static InferenceTestHost host = null!;

    private const string FluxGuidanceT2I =
        "BasicGuider,BasicScheduler,CLIPTextEncode,DualCLIPLoader,EmptySD3LatentImage,FluxGuidance,KSamplerSelect,PreviewImage,RandomNoise,SamplerCustomAdvanced,UNETLoader,VAEDecode,VAELoader";
    private const string FluxGuidanceI2I =
        "BasicGuider,BasicScheduler,CLIPTextEncode,DualCLIPLoader,FluxGuidance,KSamplerSelect,LoadImage,PreviewImage,RandomNoise,SamplerCustomAdvanced,UNETLoader,VAEDecode,VAEEncode,VAELoader";
    private const string Flux2T2I =
        "CFGGuider,CLIPLoader,CLIPTextEncode,EmptyFlux2LatentImage,Flux2Scheduler,KSamplerSelect,PreviewImage,RandomNoise,SamplerCustomAdvanced,UNETLoader,VAEDecode,VAELoader";
    private const string Flux2I2I =
        "CFGGuider,CLIPLoader,CLIPTextEncode,Flux2Scheduler,KSamplerSelect,LoadImage,PreviewImage,RandomNoise,SamplerCustomAdvanced,UNETLoader,VAEDecode,VAEEncode,VAELoader";
    private const string AuraFlowT2I =
        "CLIPLoader,CLIPTextEncode,EmptySD3LatentImage,KSampler,ModelSamplingAuraFlow,PreviewImage,UNETLoader,VAEDecode,VAELoader";
    private const string AuraFlowI2I =
        "CLIPLoader,CLIPTextEncode,ImageScale,KSampler,LoadImage,ModelSamplingAuraFlow,PreviewImage,UNETLoader,VAEDecode,VAEEncode,VAELoader";
    private const string HiDreamT2I =
        "CLIPTextEncode,EmptySD3LatentImage,KSampler,ModelSamplingSD3,PreviewImage,QuadrupleCLIPLoader,UNETLoader,VAEDecode,VAELoader";
    private const string HiDreamI2I =
        "CLIPTextEncode,ImageScale,KSampler,LoadImage,ModelSamplingSD3,PreviewImage,QuadrupleCLIPLoader,UNETLoader,VAEDecode,VAEEncode,VAELoader";
    private const string KSamplerSD3LatentT2I =
        "CLIPLoader,CLIPTextEncode,EmptySD3LatentImage,KSampler,PreviewImage,UNETLoader,VAEDecode,VAELoader";
    private const string KSamplerFlux2LatentT2I =
        "CLIPLoader,CLIPTextEncode,EmptyFlux2LatentImage,KSampler,PreviewImage,UNETLoader,VAEDecode,VAELoader";
    private const string KSamplerI2I =
        "CLIPLoader,CLIPTextEncode,ImageScale,KSampler,LoadImage,PreviewImage,UNETLoader,VAEDecode,VAEEncode,VAELoader";

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) => host = new InferenceTestHost();

    [ClassCleanup]
    public static void ClassCleanup() => host?.Dispose();

    [TestInitialize]
    public void TestInitialize()
    {
        // View models observe on the current SynchronizationContext when constructed
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
    }

    [TestMethod]
    [DataRow("Flux", null, "flux1-dev.safetensors", FluxGuidanceT2I)]
    [DataRow("Flux", null, "flux1-dev-Q8_0.gguf", FluxGuidanceT2I)]
    [DataRow("Flux2", null, "flux2-dev.safetensors", Flux2T2I)]
    [DataRow("ZImageTurbo", null, "z_image_turbo.safetensors", AuraFlowT2I)]
    [DataRow(
        "Anima",
        null,
        "anima-base-v1.0.safetensors",
        "CLIPLoader,CLIPTextEncode,EmptyLatentImage,KSampler,PreviewImage,UNETLoader,VAEDecode,VAELoader"
    )]
    [DataRow("HiDream", null, "hidream_i1_full.safetensors", HiDreamT2I)]
    [DataRow("QwenImage", null, "qwen_image.safetensors", AuraFlowT2I)]
    [DataRow("QwenImage", null, "qwen_image-Q8_0.gguf", AuraFlowT2I)]
    [DataRow("Krea2Turbo", null, "krea2_turbo.safetensors", KSamplerSD3LatentT2I)]
    [DataRow("MossImage", null, "moss_image_sft.safetensors", KSamplerFlux2LatentT2I)]
    [DataRow("Custom", "flux", "model.safetensors", FluxGuidanceT2I)]
    [DataRow("Custom", "flux2", "model.safetensors", Flux2T2I)]
    [DataRow("Custom", "qwen_image", "model.safetensors", AuraFlowT2I)]
    [DataRow("Custom", "krea2", "model.safetensors", KSamplerSD3LatentT2I)]
    [DataRow("Custom", "hidream", "model.safetensors", HiDreamT2I)]
    [DataRow("Custom", "moss_image", "model.safetensors", KSamplerFlux2LatentT2I)]
    [DataRow("Custom", "chroma", "model.safetensors", KSamplerSD3LatentT2I)]
    [DataRow(
        "Custom",
        "sd3",
        "model.safetensors",
        "CLIPTextEncode,EmptySD3LatentImage,KSampler,PreviewImage,TripleCLIPLoader,UNETLoader,VAEDecode,VAELoader"
    )]
    public void TextToImage_ProfileBuildsExpectedNodes(
        string profile,
        string? clipType,
        string unetFile,
        string expectedNodes
    )
    {
        var vm = host.Create<InferenceTextToImageViewModel>();

        SelectModels(vm.ModelCardViewModel, profile, clipType, unetFile);

        AssertNodeTypes(WithUnetLoaderFor(unetFile, expectedNodes), BuildNodeTypes(vm));
    }

    [TestMethod]
    [DataRow("Flux", null, "flux1-dev.safetensors", FluxGuidanceI2I)]
    [DataRow("Flux", null, "flux1-dev-Q8_0.gguf", FluxGuidanceI2I)]
    [DataRow("Flux2", null, "flux2-dev.safetensors", Flux2I2I)]
    [DataRow("ZImageTurbo", null, "z_image_turbo.safetensors", AuraFlowI2I)]
    [DataRow("Anima", null, "anima-base-v1.0.safetensors", KSamplerI2I)]
    [DataRow("HiDream", null, "hidream_i1_full.safetensors", HiDreamI2I)]
    [DataRow("QwenImage", null, "qwen_image.safetensors", AuraFlowI2I)]
    [DataRow("QwenImage", null, "qwen_image-Q8_0.gguf", AuraFlowI2I)]
    [DataRow("Krea2Turbo", null, "krea2_turbo.safetensors", KSamplerI2I)]
    [DataRow("MossImage", null, "moss_image_sft.safetensors", KSamplerI2I)]
    [DataRow("Custom", "flux", "model.safetensors", FluxGuidanceI2I)]
    [DataRow("Custom", "flux2", "model.safetensors", Flux2I2I)]
    [DataRow("Custom", "qwen_image", "model.safetensors", AuraFlowI2I)]
    [DataRow("Custom", "krea2", "model.safetensors", KSamplerI2I)]
    [DataRow("Custom", "hidream", "model.safetensors", HiDreamI2I)]
    [DataRow("Custom", "moss_image", "model.safetensors", KSamplerI2I)]
    [DataRow("Custom", "chroma", "model.safetensors", KSamplerI2I)]
    [DataRow(
        "Custom",
        "sd3",
        "model.safetensors",
        "CLIPTextEncode,ImageScale,KSampler,LoadImage,PreviewImage,TripleCLIPLoader,UNETLoader,VAEDecode,VAEEncode,VAELoader"
    )]
    public void ImageToImage_ProfileBuildsExpectedNodes(
        string profile,
        string? clipType,
        string unetFile,
        string expectedNodes
    )
    {
        var vm = host.Create<InferenceImageToImageViewModel>();

        var imageSource = new ImageSource(new FilePath(host.InputImagePath));
        // Building the graph reads the image's cached hash
        imageSource.GetBlake3HashAsync().GetAwaiter().GetResult();
        vm.SelectImageCardViewModel.ImageSource = imageSource;
        vm.SelectImageCardViewModel.CurrentBitmapSize = new Size(1, 1);

        SelectModels(vm.ModelCardViewModel, profile, clipType, unetFile);

        AssertNodeTypes(WithUnetLoaderFor(unetFile, expectedNodes), BuildNodeTypes(vm));
    }

    [TestMethod]
    public void SkipLayerGuidance_PatchesModelBeforeKSampler()
    {
        var vm = host.Create<InferenceTextToImageViewModel>();

        SelectModels(vm.ModelCardViewModel, "MossImage", null, "moss_image_sft.safetensors");
        vm.SamplerCardViewModel.ModulesCardViewModel.AddModule<SkipLayerGuidanceModule>(m =>
            m.IsEnabled = true
        );

        var nodes = BuildNodes(vm);

        var slgName = nodes.Single(n => n.Value.ClassType == "SkipLayerGuidanceDiTSimple").Key;
        var sampler = nodes.Values.Single(n => n.ClassType == "KSampler");
        // Connections are normalized to [node name, output index]
        var samplerModel = (object[])sampler.Inputs["model"]!;
        Assert.AreEqual(slgName, samplerModel[0]);
    }

    private static string WithUnetLoaderFor(string unetFile, string expectedNodes) =>
        unetFile.EndsWith(".gguf", StringComparison.Ordinal)
            ? expectedNodes.Replace("UNETLoader", "UnetLoaderGGUF", StringComparison.Ordinal)
            : expectedNodes;

    private static HybridModelFile LocalModel(string relativePath, SharedFolderType type) =>
        HybridModelFile.FromLocal(
            new LocalModelFile { RelativePath = relativePath, SharedFolderType = type }
        );

    private static void SelectModels(
        ModelCardViewModel modelCard,
        string profile,
        string? clipType,
        string unetFile
    )
    {
        modelCard.SelectedModelLoader = ModelLoader.Unet;
        modelCard.SelectedUnetModel = LocalModel(unetFile, SharedFolderType.DiffusionModels);
        modelCard.SelectedWorkflowProfile = InferenceWorkflowProfile.FromKey(profile);
        if (clipType is not null)
            modelCard.SelectedClipType = clipType;

        for (var i = 0; i < modelCard.TextEncoders.Count; i++)
        {
            modelCard.TextEncoders[i].SelectedModel = LocalModel(
                $"text_encoder_{i}.safetensors",
                SharedFolderType.TextEncoders
            );
        }
        modelCard.SelectedVae = LocalModel("vae.safetensors", SharedFolderType.VAE);
    }

    private static NodeDictionary BuildNodes(InferenceGenerationViewModelBase vm) =>
        InferenceTestHost.BuildNodes(vm);

    private static string BuildNodeTypes(InferenceGenerationViewModelBase vm) =>
        string.Join(
            ",",
            BuildNodes(vm).Values.Select(n => n.ClassType).Distinct().Order(StringComparer.Ordinal)
        );

    private static void AssertNodeTypes(string expected, string actual)
    {
        var expectedSet = expected.Split(',').ToHashSet();
        var actualSet = actual.Split(',').ToHashSet();
        Assert.AreEqual(
            expected,
            actual,
            $"Missing: [{string.Join(",", expectedSet.Except(actualSet))}] "
                + $"Unexpected: [{string.Join(",", actualSet.Except(expectedSet))}]"
        );
    }
}
