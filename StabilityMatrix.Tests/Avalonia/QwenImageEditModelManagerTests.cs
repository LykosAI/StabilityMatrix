using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class QwenImageEditModelManagerTests
{
    private readonly QwenImageEditModelManager manager = new();

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

    private static readonly HybridModelFile QwenUnet = LocalModel(
        "qwen_image_edit_2511_fp8mixed.safetensors",
        SharedFolderType.DiffusionModels
    );
    private static readonly HybridModelFile QwenVae = LocalModel(
        "qwen_image_vae.safetensors",
        SharedFolderType.VAE
    );
    private static readonly HybridModelFile Vl7BEncoder = LocalModel(
        "qwen_2.5_vl_7b_fp8_scaled.safetensors",
        SharedFolderType.TextEncoders
    );
    private static readonly HybridModelFile Vl3BEncoder = LocalModel(
        "qwen_2.5_vl_3b_instruct.safetensors",
        SharedFolderType.TextEncoders
    );

    [TestMethod]
    public void AreModelsAvailable_WrongSizeVlEncoder_ReportsMissing()
    {
        // A 3B VL encoder (hidden size 2048) loads but dies mid-sampling with
        // "expected input with shape [*, 3584]" - it must not count as available.
        var clientManager = CreateClientManager(unet: [QwenUnet], vae: [QwenVae], clip: [Vl3BEncoder]);

        Assert.IsFalse(manager.AreModelsAvailable(clientManager));

        var encoder = manager.GetMissingModels(clientManager).Single();
        Assert.AreEqual(SharedFolderType.TextEncoders, encoder.ContextType);
        StringAssert.Contains(encoder.FileName, "7b");
    }

    [TestMethod]
    public void AreModelsAvailable_SevenBEncoder_IsAvailable()
    {
        var clientManager = CreateClientManager(unet: [QwenUnet], vae: [QwenVae], clip: [Vl7BEncoder]);

        Assert.IsTrue(manager.AreModelsAvailable(clientManager));
        Assert.AreEqual(0, manager.GetMissingModels(clientManager).Count());
    }

    [TestMethod]
    public void SelectModels_PrefersSevenBOverOtherSizes()
    {
        // Collection order intentionally puts the 3B first - selection must not be
        // "first VL encoder wins".
        var clientManager = CreateClientManager(
            unet: [QwenUnet],
            vae: [QwenVae],
            clip: [Vl3BEncoder, Vl7BEncoder]
        );

        var selected = manager.SelectModels(clientManager);

        Assert.AreEqual(Vl7BEncoder, selected.ClipModel);
    }

    [TestMethod]
    public void SelectModels_OnlyWrongSizeEncoder_ThrowsWithActionableMessage()
    {
        var clientManager = CreateClientManager(unet: [QwenUnet], vae: [QwenVae], clip: [Vl3BEncoder]);

        var ex = Assert.ThrowsException<InvalidOperationException>(() => manager.SelectModels(clientManager));

        StringAssert.Contains(ex.Message, "7B");
        StringAssert.Contains(ex.Message, "qwen_2.5_vl_7b_fp8_scaled.safetensors");
    }

    [TestMethod]
    public void SelectModels_UnsizedVlEncoder_IsAcceptedAsFallback()
    {
        // A VL encoder with no size hint is likely a renamed 7B - give it a shot rather
        // than refusing to run.
        var renamed = LocalModel("qwen_2.5_vl_fp8_scaled.safetensors", SharedFolderType.TextEncoders);
        var clientManager = CreateClientManager(unet: [QwenUnet], vae: [QwenVae], clip: [renamed]);

        var selected = manager.SelectModels(clientManager);

        Assert.AreEqual(renamed, selected.ClipModel);
    }
}
