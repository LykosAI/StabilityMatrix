using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class Flux2KleinModelManagerTests
{
    private readonly Flux2KleinModelManager manager = new();

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

    private static readonly HybridModelFile Klein9BUnet = LocalModel(
        "flux-2-klein-9b.safetensors",
        SharedFolderType.DiffusionModels
    );
    private static readonly HybridModelFile Klein4BUnet = LocalModel(
        "flux-2-klein-4b.safetensors",
        SharedFolderType.DiffusionModels
    );
    private static readonly HybridModelFile Flux2Vae = LocalModel(
        "flux2-vae.safetensors",
        SharedFolderType.VAE
    );
    private static readonly HybridModelFile Qwen38BEncoder = LocalModel(
        "qwen_3_8b.safetensors",
        SharedFolderType.TextEncoders
    );
    private static readonly HybridModelFile Qwen34BEncoder = LocalModel(
        "qwen_3_4b.safetensors",
        SharedFolderType.TextEncoders
    );

    [TestMethod]
    public void GetMissingModels_PartialNineB_OffersEightBEncoderNotFourB()
    {
        // User has the 9B UNET + VAE but hasn't downloaded the text encoder yet.
        var clientManager = CreateClientManager(unet: [Klein9BUnet], vae: [Flux2Vae], clip: []);

        var missing = manager.GetMissingModels(clientManager).ToList();

        // Only the encoder should be missing, and it must be the 8B one to pair with the 9B UNET.
        Assert.AreEqual(1, missing.Count);
        var encoder = missing.Single();
        Assert.AreEqual(SharedFolderType.TextEncoders, encoder.ContextType);
        StringAssert.Contains(encoder.FileName, "qwen_3_8b");
        Assert.IsFalse(
            encoder.FileName.Contains("qwen_3_4b"),
            "Should not offer the 4B encoder for a 9B UNET"
        );
    }

    [TestMethod]
    public void GetMissingModelNames_PartialNineB_LabelsEncoderAsEightB()
    {
        var clientManager = CreateClientManager(unet: [Klein9BUnet], vae: [Flux2Vae], clip: []);

        var names = manager.GetMissingModelNames(clientManager).ToList();

        CollectionAssert.AreEquivalent(new[] { "Qwen3 8B text encoder" }, names);
    }

    [TestMethod]
    public void GetMissingModels_CompleteNineB_OffersNothing()
    {
        var clientManager = CreateClientManager(unet: [Klein9BUnet], vae: [Flux2Vae], clip: [Qwen38BEncoder]);

        Assert.IsTrue(manager.AreModelsAvailable(clientManager));
        Assert.AreEqual(0, manager.GetMissingModels(clientManager).Count());
    }

    [TestMethod]
    public void GetMissingModels_PartialFourB_OffersFourBEncoder()
    {
        var clientManager = CreateClientManager(unet: [Klein4BUnet], vae: [Flux2Vae], clip: []);

        var encoder = manager.GetMissingModels(clientManager).Single();

        Assert.AreEqual(SharedFolderType.TextEncoders, encoder.ContextType);
        StringAssert.Contains(encoder.FileName, "qwen_3_4b");
    }

    [TestMethod]
    public void GetMissingModels_NineBUnetWithOnlyFourBEncoder_OffersEightBEncoder()
    {
        // User ran Klein 4B before (has the 4B encoder), then dropped in a 9B UNET. The 4B
        // encoder doesn't pair with the 9B UNET, so the 8B encoder must still be offered.
        var clientManager = CreateClientManager(unet: [Klein9BUnet], vae: [Flux2Vae], clip: [Qwen34BEncoder]);

        Assert.IsFalse(
            manager.AreModelsAvailable(clientManager),
            "A 4B encoder must not satisfy a 9B UNET's requirements"
        );

        var encoder = manager.GetMissingModels(clientManager).Single();
        Assert.AreEqual(SharedFolderType.TextEncoders, encoder.ContextType);
        StringAssert.Contains(encoder.FileName, "qwen_3_8b");
    }

    [TestMethod]
    public void AreModelsAvailable_PreferredUnetOverridesInstalledVariant()
    {
        // Both UNETs installed but only the 4B encoder present: availability depends on
        // which UNET the user has actually selected in the dropdown.
        var clientManager = CreateClientManager(
            unet: [Klein4BUnet, Klein9BUnet],
            vae: [Flux2Vae],
            clip: [Qwen34BEncoder]
        );

        Assert.IsTrue(manager.AreModelsAvailable(clientManager, Klein4BUnet));
        Assert.IsFalse(manager.AreModelsAvailable(clientManager, Klein9BUnet));

        var encoder = manager.GetMissingModels(clientManager, Klein9BUnet).Single();
        StringAssert.Contains(encoder.FileName, "qwen_3_8b");
    }

    [TestMethod]
    public void GetMissingModels_FreshInstall_OffersFourBSet()
    {
        // Nothing installed - default to the freely downloadable Apache 2.0 4B set.
        var clientManager = CreateClientManager();

        var missing = manager.GetMissingModels(clientManager).ToList();
        var names = manager.GetMissingModelNames(clientManager).ToList();

        Assert.AreEqual(3, missing.Count);
        Assert.IsTrue(
            missing.Any(m => m.ContextType is SharedFolderType.DiffusionModels && m.FileName.Contains("4b"))
        );
        Assert.IsTrue(
            missing.Any(m =>
                m.ContextType is SharedFolderType.TextEncoders && m.FileName.Contains("qwen_3_4b")
            )
        );
        CollectionAssert.Contains(names, "Flux.2 Klein 4B UNET");
        CollectionAssert.Contains(names, "Qwen3 4B text encoder");
    }
}
