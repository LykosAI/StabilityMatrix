using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

/// <summary>
/// Repro for the post-refactor smoke report: flipping through UNETs of different families
/// with a realistic encoder library (clip_l installed alongside the qwen encoders) must
/// re-derive clip type, slot count, and encoder auto-selection every time.
/// </summary>
[TestClass]
public class ModelCardProfileFlipReproTests
{
    private static HybridModelFile LocalModel(string fileName, SharedFolderType type) =>
        HybridModelFile.FromLocal(new LocalModelFile { RelativePath = fileName, SharedFolderType = type });

    private static ModelCardViewModel CreateViewModel(
        IEnumerable<HybridModelFile> clipModels,
        IEnumerable<HybridModelFile> vaeModels
    )
    {
        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.ClipModels.Returns(new ObservableCollectionExtended<HybridModelFile>(clipModels));
        clientManager.VaeModels.Returns(new ObservableCollectionExtended<HybridModelFile>(vaeModels));
        var vmFactory = Substitute.For<IServiceManager<ViewModelBase>>();
        return new ModelCardViewModel(
            clientManager,
            vmFactory,
            new TabContext(),
            Substitute.For<ISettingsManager>(),
            Substitute.For<IModelIndexService>(),
            Substitute.For<INotificationService>(),
            new ModelOrganizationService()
        );
    }

    [TestMethod]
    public void FlippingUnets_RederivesClipTypeSlotsAndEncoders()
    {
        var clipL = LocalModel("clip_l.safetensors", SharedFolderType.TextEncoders);
        var t5 = LocalModel("t5xxl_fp8_e4m3fn_scaled.safetensors", SharedFolderType.TextEncoders);
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var qwenVl = LocalModel("qwen_2.5_vl_7b_fp8_scaled.safetensors", SharedFolderType.TextEncoders);

        var fluxVae = LocalModel("ae.safetensors", SharedFolderType.VAE);
        var flux2Vae = LocalModel("flux2-vae.safetensors", SharedFolderType.VAE);
        var qwenVae = LocalModel("qwen_image_vae.safetensors", SharedFolderType.VAE);

        var vm = CreateViewModel([clipL, t5, qwen4B, qwenVl], [fluxVae, flux2Vae, qwenVae]);

        // 1. Flux.1 UNET: dual clip_l + t5, 2 slots
        vm.SelectedUnifiedModel = LocalModel("flux1-dev-fp8.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreSame(InferenceWorkflowProfile.Flux, vm.ResolvedWorkflowProfile);
        Assert.AreEqual("flux", vm.SelectedClipType);
        Assert.AreEqual(2, vm.TextEncoders.Count, "Flux should have 2 encoder slots");
        Assert.AreEqual(clipL, vm.TextEncoders[0].SelectedModel);
        Assert.AreEqual(t5, vm.TextEncoders[1].SelectedModel);

        // 2. Flux.2 Klein UNET: single qwen_3_4b — clip_l must NOT survive the flip
        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreSame(InferenceWorkflowProfile.Flux2, vm.ResolvedWorkflowProfile);
        Assert.AreEqual("flux2", vm.SelectedClipType);
        Assert.AreEqual(1, vm.TextEncoders.Count, "Klein should have 1 encoder slot");
        Assert.AreEqual(
            qwen4B,
            vm.TextEncoders[0].SelectedModel,
            "Klein must pair with qwen_3_4b, not clip_l"
        );
        Assert.AreEqual(flux2Vae, vm.SelectedVae);

        // 3. Qwen Image UNET: single qwen 2.5 VL
        vm.SelectedUnifiedModel = LocalModel(
            "qwen_image_2512_fp8_e4m3fn.safetensors",
            SharedFolderType.DiffusionModels
        );
        Assert.AreSame(InferenceWorkflowProfile.QwenImage, vm.ResolvedWorkflowProfile);
        Assert.AreEqual("qwen_image", vm.SelectedClipType);
        Assert.AreEqual(1, vm.TextEncoders.Count);
        Assert.AreEqual(qwenVl, vm.TextEncoders[0].SelectedModel);
        Assert.AreEqual(qwenVae, vm.SelectedVae);

        // 4. Back to Flux.1: slots grow back to 2 and clip_l + t5 return
        vm.SelectedUnifiedModel = LocalModel("flux1-dev-fp8.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreSame(InferenceWorkflowProfile.Flux, vm.ResolvedWorkflowProfile);
        Assert.AreEqual("flux", vm.SelectedClipType);
        Assert.AreEqual(2, vm.TextEncoders.Count, "Flipping back to Flux should restore 2 slots");
        Assert.AreEqual(clipL, vm.TextEncoders[0].SelectedModel);
    }

    /// <summary>
    /// The auto-selected set is session state. After save + restore (app restart), the
    /// restored auto-picks must be re-adopted as replaceable defaults — otherwise flipping
    /// to a different family keeps the old family's encoders and slot count forever
    /// ("Klein keeps clip_l after restart").
    /// </summary>
    [TestMethod]
    public void AfterStateRestore_FlippingUnets_StillRepairsEncoders()
    {
        var clipL = LocalModel("clip_l.safetensors", SharedFolderType.TextEncoders);
        var t5 = LocalModel("t5xxl_fp8_e4m3fn_scaled.safetensors", SharedFolderType.TextEncoders);
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var fluxVae = LocalModel("ae.safetensors", SharedFolderType.VAE);
        var flux2Vae = LocalModel("flux2-vae.safetensors", SharedFolderType.VAE);
        var fluxUnet = LocalModel("flux1-dev-fp8.safetensors", SharedFolderType.DiffusionModels);
        var kleinUnet = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);

        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.ClipModels.Returns(
            new ObservableCollectionExtended<HybridModelFile>([clipL, t5, qwen4B])
        );
        clientManager.VaeModels.Returns(
            new ObservableCollectionExtended<HybridModelFile>([fluxVae, flux2Vae])
        );
        clientManager.UnetModels.Returns(
            new ObservableCollectionExtended<HybridModelFile>([fluxUnet, kleinUnet])
        );

        ModelCardViewModel CreateVm() =>
            new(
                clientManager,
                Substitute.For<IServiceManager<ViewModelBase>>(),
                new TabContext(),
                Substitute.For<ISettingsManager>(),
                Substitute.For<IModelIndexService>(),
                Substitute.For<INotificationService>(),
                new ModelOrganizationService()
            );

        // Session 1: Flux UNET auto-fills clip_l + t5, then the project is saved.
        var vm1 = CreateVm();
        vm1.SelectedUnifiedModel = fluxUnet;
        Assert.AreEqual(clipL, vm1.TextEncoders[0].SelectedModel);
        var state = vm1.SaveStateToJsonObject();

        // Session 2 (fresh VM = app restart): restore, then flip to a Klein UNET.
        var vm2 = CreateVm();
        vm2.LoadStateFromJsonObject(state);
        Assert.AreEqual(2, vm2.TextEncoders.Count, "Restored state should keep Flux's 2 slots");
        Assert.AreEqual(clipL, vm2.TextEncoders[0].SelectedModel);

        vm2.SelectedUnifiedModel = kleinUnet;

        Assert.AreSame(InferenceWorkflowProfile.Flux2, vm2.ResolvedWorkflowProfile);
        Assert.AreEqual("flux2", vm2.SelectedClipType);
        Assert.AreEqual(1, vm2.TextEncoders.Count, "Slot count must resize after restore + flip");
        Assert.AreEqual(
            qwen4B,
            vm2.TextEncoders[0].SelectedModel,
            "Restored clip_l must be replaced by Klein's qwen_3_4b"
        );
        Assert.AreEqual(flux2Vae, vm2.SelectedVae, "Restored Flux VAE must be replaced by the Flux.2 VAE");
    }

    /// <summary>
    /// The seeding must not touch genuine user picks: a custom encoder that matches no
    /// profile default stays selected across restore + model flips.
    /// </summary>
    [TestMethod]
    public void AfterStateRestore_UserPickedEncoder_StaysProtected()
    {
        var customEncoder = LocalModel("my_finetuned_encoder.safetensors", SharedFolderType.TextEncoders);
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var fluxUnet = LocalModel("flux1-dev-fp8.safetensors", SharedFolderType.DiffusionModels);
        var kleinUnet = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);

        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.ClipModels.Returns(
            new ObservableCollectionExtended<HybridModelFile>([customEncoder, qwen4B])
        );
        clientManager.VaeModels.Returns(new ObservableCollectionExtended<HybridModelFile>([]));
        clientManager.UnetModels.Returns(
            new ObservableCollectionExtended<HybridModelFile>([fluxUnet, kleinUnet])
        );

        ModelCardViewModel CreateVm() =>
            new(
                clientManager,
                Substitute.For<IServiceManager<ViewModelBase>>(),
                new TabContext(),
                Substitute.For<ISettingsManager>(),
                Substitute.For<IModelIndexService>(),
                Substitute.For<INotificationService>(),
                new ModelOrganizationService()
            );

        var vm1 = CreateVm();
        vm1.SelectedUnifiedModel = fluxUnet;
        vm1.TextEncoders[0].SelectedModel = customEncoder;
        var state = vm1.SaveStateToJsonObject();

        var vm2 = CreateVm();
        vm2.LoadStateFromJsonObject(state);
        vm2.SelectedUnifiedModel = kleinUnet;

        Assert.AreEqual(
            customEncoder,
            vm2.TextEncoders[0].SelectedModel,
            "A restored user pick that matches no profile default must never be replaced"
        );
    }

    [TestMethod]
    public void FlippingToHiDream_GrowsToFourSlots()
    {
        var clipL = LocalModel("clip_l.safetensors", SharedFolderType.TextEncoders);
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var vm = CreateViewModel([clipL, qwen4B], []);

        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreEqual(1, vm.TextEncoders.Count);

        vm.SelectedUnifiedModel = LocalModel(
            "hidream_i1_dev_fp8.safetensors",
            SharedFolderType.DiffusionModels
        );
        Assert.AreSame(InferenceWorkflowProfile.HiDream, vm.ResolvedWorkflowProfile);
        Assert.AreEqual("hidream", vm.SelectedClipType);
        Assert.AreEqual(4, vm.TextEncoders.Count, "HiDream should have 4 encoder slots");
    }
}
