using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class Krea2ImageLabTests
{
    private readonly Krea2ModelManager manager = new();

    private static HybridModelFile LocalModel(string fileName, SharedFolderType type) =>
        HybridModelFile.FromLocal(new LocalModelFile { RelativePath = fileName, SharedFolderType = type });

    private static IInferenceClientManager CreateClientManager(
        IEnumerable<HybridModelFile>? unet = null,
        IEnumerable<HybridModelFile>? vae = null,
        IEnumerable<HybridModelFile>? clip = null
    )
    {
        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.UnetModels.Returns(new ObservableCollectionExtended<HybridModelFile>(unet ?? []));
        clientManager.VaeModels.Returns(new ObservableCollectionExtended<HybridModelFile>(vae ?? []));
        clientManager.ClipModels.Returns(new ObservableCollectionExtended<HybridModelFile>(clip ?? []));
        return clientManager;
    }

    private static readonly HybridModelFile KreaUnet = LocalModel(
        "DaSiWa-Krea2-Turbo-UC-DarkDesire-v3_int4_convrot.safetensors",
        SharedFolderType.DiffusionModels
    );
    private static readonly HybridModelFile Wan21Vae = LocalModel(
        "wan_2.1_vae.safetensors",
        SharedFolderType.VAE
    );
    private static readonly HybridModelFile Qwen3Vl4B = LocalModel(
        "qwen3vl_4b_fp8_scaled.safetensors",
        SharedFolderType.TextEncoders
    );

    [TestMethod]
    public void IsKreaUnet_MatchesKrea2_ButNotFluxKreaOrKlein()
    {
        Assert.IsTrue(Krea2ModelManager.IsKreaUnet(KreaUnet));
        Assert.IsTrue(
            Krea2ModelManager.IsKreaUnet(LocalModel("krea-2-turbo.safetensors", SharedFolderType.DiffusionModels))
        );

        // Flux.1 Krea is a different architecture — selecting it would fail at sampling.
        Assert.IsFalse(
            Krea2ModelManager.IsKreaUnet(
                LocalModel("flux1-krea-dev.safetensors", SharedFolderType.DiffusionModels)
            )
        );
        Assert.IsFalse(
            Krea2ModelManager.IsKreaUnet(
                LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels)
            )
        );
    }

    [TestMethod]
    public void IsKreaTextEncoder_RequiresVisionLanguageEncoder_NotKleinsPlainQwen3()
    {
        Assert.IsTrue(Krea2ModelManager.IsKreaTextEncoder("qwen3vl_4b_fp8_scaled.safetensors"));
        // Klein's encoder looks similar but cannot feed Krea 2.
        Assert.IsFalse(Krea2ModelManager.IsKreaTextEncoder("qwen_3_4b.safetensors"));
    }

    [TestMethod]
    public void GetMissingModelNames_ReportsEachMissingPiece()
    {
        var empty = CreateClientManager();
        Assert.AreEqual(3, manager.GetMissingModelNames(empty).Count());
        Assert.IsFalse(manager.AreModelsAvailable(empty));

        var onlyUnet = CreateClientManager(unet: [KreaUnet]);
        Assert.AreEqual(2, manager.GetMissingModelNames(onlyUnet).Count());

        var all = CreateClientManager(unet: [KreaUnet], vae: [Wan21Vae], clip: [Qwen3Vl4B]);
        Assert.AreEqual(0, manager.GetMissingModelNames(all).Count());
        Assert.IsTrue(manager.AreModelsAvailable(all));

        // Never offers downloads: only reports what is missing.
        Assert.AreEqual(0, manager.GetMissingModels(empty).Count());
    }

    [TestMethod]
    public void Build_UsesSingleKrea2ClipLoader_AndTurboDefaults()
    {
        var clientManager = CreateClientManager(unet: [KreaUnet], vae: [Wan21Vae], clip: [Qwen3Vl4B]);

        var nodes = Krea2WorkflowBuilder.Build(
            new ImageGenerationRequest { TextPrompt = "a forest" },
            clientManager,
            customUnetModel: KreaUnet
        );

        var classTypes = nodes.Values.Select(n => n.ClassType).ToList();

        // The bug that motivated Krea 2 support: a dual loader rejects type 'krea2'.
        CollectionAssert.DoesNotContain(classTypes, "DualCLIPLoader");
        Assert.AreEqual(1, classTypes.Count(t => t == "CLIPLoader"));

        var clipLoader = nodes.Values.Single(n => n.ClassType == "CLIPLoader");
        Assert.AreEqual("krea2", clipLoader.Inputs["type"]);
        StringAssert.EndsWith((string)clipLoader.Inputs["clip_name"]!, "qwen3vl_4b_fp8_scaled.safetensors");

        var sampler = nodes.Values.Single(n => n.ClassType == "KSampler");
        Assert.AreEqual(8, sampler.Inputs["steps"]);
        Assert.AreEqual(1.0, sampler.Inputs["cfg"]);

        CollectionAssert.Contains(classTypes, "EmptySD3LatentImage");
        CollectionAssert.DoesNotContain(classTypes, "FluxGuidance");
    }

    [TestMethod]
    public void Build_HonorsStepsCfgAndDimensionsOverrides()
    {
        var clientManager = CreateClientManager(unet: [KreaUnet], vae: [Wan21Vae], clip: [Qwen3Vl4B]);

        var nodes = Krea2WorkflowBuilder.Build(
            new ImageGenerationRequest { TextPrompt = "x" },
            clientManager,
            customUnetModel: KreaUnet,
            width: 768,
            height: 1024,
            steps: 20,
            cfg: 4.5
        );

        var sampler = nodes.Values.Single(n => n.ClassType == "KSampler");
        Assert.AreEqual(20, sampler.Inputs["steps"]);
        Assert.AreEqual(4.5, sampler.Inputs["cfg"]);

        var latent = nodes.Values.Single(n => n.ClassType == "EmptySD3LatentImage");
        Assert.AreEqual(768, latent.Inputs["width"]);
        Assert.AreEqual(1024, latent.Inputs["height"]);
    }

    [TestMethod]
    public void Build_WithMissingVae_FailsWithClearMessage()
    {
        var clientManager = CreateClientManager(unet: [KreaUnet], clip: [Qwen3Vl4B]);

        var ex = Assert.ThrowsException<InvalidOperationException>(() =>
            Krea2WorkflowBuilder.Build(
                new ImageGenerationRequest { TextPrompt = "x" },
                clientManager,
                customUnetModel: KreaUnet
            )
        );
        StringAssert.Contains(ex.Message, "Wan 2.1 VAE");
    }

    [TestMethod]
    public void Krea2_IsRegisteredAsLocalProviderWithManager()
    {
        Assert.IsTrue(BananaVisionProviderIds.IsLocalProvider(BananaVisionProviderIds.Krea2));
        Assert.IsTrue(LocalProviderModelManagerRegistry.HasManager(BananaVisionProviderIds.Krea2));
    }
}
