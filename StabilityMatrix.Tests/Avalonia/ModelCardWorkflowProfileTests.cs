using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class ModelCardWorkflowProfileTests
{
    private static HybridModelFile LocalModel(string fileName, SharedFolderType type) =>
        HybridModelFile.FromLocal(new LocalModelFile { RelativePath = fileName, SharedFolderType = type });

    private static ModelCardViewModel CreateViewModel(
        IEnumerable<HybridModelFile>? clipModels = null,
        IEnumerable<HybridModelFile>? vaeModels = null
    )
    {
        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.ClipModels.Returns(new ObservableCollectionExtended<HybridModelFile>(clipModels ?? []));
        clientManager.VaeModels.Returns(new ObservableCollectionExtended<HybridModelFile>(vaeModels ?? []));
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
    public void SwitchingToDefaultCheckpoint_WithUnetFolderModel_KeepsLoaderAndWarns()
    {
        var vm = CreateViewModel();
        var anima = LocalModel("anima-base-v1.0.safetensors", SharedFolderType.DiffusionModels);

        vm.SelectedModelLoader = ModelLoader.Unet;
        vm.SelectedUnetModel = anima;

        Assert.IsTrue(vm.ShowEncoderSection, "Encoder section should show for a UNet-folder model");

        // User picks "Default / Checkpoint" — but the file physically lives in DiffusionModels,
        // so CheckpointLoader could never load it. The loader must NOT flip (that would build a
        // guaranteed-invalid workflow); instead the mismatch warning explains the situation.
        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.DefaultCheckpoint;

        Assert.AreEqual(ModelLoader.Unet, vm.SelectedModelLoader);
        Assert.AreEqual(anima, vm.SelectedUnetModel);
        Assert.IsTrue(vm.ShowWorkflowProfileWarning, "Profile/folder mismatch should surface a warning");
        StringAssert.Contains(vm.WorkflowProfileWarningText, "DiffusionModels");
    }

    [TestMethod]
    public void FluxProfile_WithAllInOneCheckpoint_KeepsLoaderWithoutWarning()
    {
        var vm = CreateViewModel();
        var fluxAllInOne = LocalModel("flux1-dev-fp8.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = fluxAllInOne;

        // Flux supports both split UNETs and all-in-one checkpoints. Choosing the Flux
        // workflow should apply its sampling defaults without claiming this file is misplaced.
        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Flux;

        Assert.AreEqual(ModelLoader.Default, vm.SelectedModelLoader);
        Assert.AreEqual(fluxAllInOne, vm.SelectedModel);
        Assert.IsFalse(vm.ShowWorkflowProfileWarning);
        Assert.IsNull(vm.MoveModelToRecommendedFolderText);
    }

    [TestMethod]
    public void SwitchingProfiles_WithNoModelSelected_FlipsLoaderFreely()
    {
        var vm = CreateViewModel();

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Flux;
        Assert.AreEqual(ModelLoader.Unet, vm.SelectedModelLoader);
        Assert.IsTrue(vm.ShowEncoderSection);
        Assert.IsTrue(vm.ShowPrecisionSelection);

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.DefaultCheckpoint;
        Assert.AreEqual(ModelLoader.Default, vm.SelectedModelLoader);
        Assert.IsFalse(vm.ShowEncoderSection, "Text encoders should be hidden for a checkpoint");
        Assert.IsFalse(vm.ShowPrecisionSelection, "Precision should be hidden for a checkpoint");
        Assert.IsFalse(vm.IsVaeSelectionEnabled, "Separate VAE selector should be cleared for a checkpoint");
        Assert.IsFalse(vm.ShowWorkflowProfileWarning, "No model selected - nothing to warn about");
    }

    [TestMethod]
    public void CustomProfile_DoesNotChangeModelLoader()
    {
        var vm = CreateViewModel();
        var anima = LocalModel("anima-base-v1.0.safetensors", SharedFolderType.DiffusionModels);

        vm.SelectedModelLoader = ModelLoader.Unet;
        vm.SelectedUnetModel = anima;

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Custom;

        // Custom leaves the loader entirely to the user.
        Assert.AreEqual(ModelLoader.Unet, vm.SelectedModelLoader);
        Assert.IsFalse(vm.ShowWorkflowProfileWarning);
    }

    [TestMethod]
    public void CustomProfile_OffersCurrentComfyDualClipTypes()
    {
        var vm = CreateViewModel();

        CollectionAssert.Contains(vm.ClipTypes, "sdxl");
        CollectionAssert.Contains(vm.ClipTypes, "hunyuan_video");
        CollectionAssert.Contains(vm.ClipTypes, "hidream");
        CollectionAssert.Contains(vm.ClipTypes, "ace");
        CollectionAssert.DoesNotContain(vm.ClipTypes, "HiDream");
    }

    [TestMethod]
    public void AnimaProfile_WithCheckpointFolderModel_KeepsLoaderAndWarns()
    {
        // Anima is UNet-only (separate qwen_3_06b encoder + Qwen Image VAE) — there is no
        // all-in-one packaging, despite Civitai filing Anima models under "Checkpoint". An
        // Anima file in StableDiffusion must warn and offer the move, like Z-Image.
        var vm = CreateViewModel();
        var animaCheckpoint = LocalModel("anima-base-v1.0.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = animaCheckpoint;

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Anima;

        Assert.AreEqual(
            ModelLoader.Default,
            vm.SelectedModelLoader,
            "Loader must not flip to UNet for a checkpoint-folder file"
        );
        Assert.IsTrue(vm.ShowWorkflowProfileWarning);
        Assert.AreEqual("Move to DiffusionModels", vm.MoveModelToRecommendedFolderText);
    }

    [TestMethod]
    public void AutoProfile_AnimaInCheckpointFolder_Warns()
    {
        var vm = CreateViewModel();
        var animaCheckpoint = LocalModel("anima-base-v1.0.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = animaCheckpoint;

        // Auto can't run a UNet-only file as a checkpoint, so the resolved profile stays
        // DefaultCheckpoint but the misplaced-file warning fires with the move offer.
        Assert.AreEqual(InferenceWorkflowProfile.DefaultCheckpoint, vm.ResolvedWorkflowProfile);
        Assert.IsTrue(vm.ShowWorkflowProfileWarning);
        StringAssert.Contains(vm.WorkflowProfileWarningText, "DiffusionModels");
    }

    [TestMethod]
    public void AutoProfile_AnimagineCheckpoint_NoAnimaDetectionOrWarning()
    {
        var vm = CreateViewModel();
        // Animagine XL is an SDXL model - "anima" must only match as its own token.
        var animagine = LocalModel("animagine-xl-v3.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = animagine;

        Assert.AreEqual(InferenceWorkflowProfile.DefaultCheckpoint, vm.ResolvedWorkflowProfile);
        Assert.IsFalse(vm.ShowWorkflowProfileWarning);
    }

    [TestMethod]
    public void AutoProfile_SdxlTaggedModelWithAnimaInName_DoesNotWarn()
    {
        var vm = CreateViewModel();
        // Explicit metadata naming a checkpoint-style base must suppress the name-based
        // misplaced-file warning entirely.
        var sdxlTagged = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = "anima_style_mix.safetensors",
                SharedFolderType = SharedFolderType.StableDiffusion,
                ConnectedModelInfo = new ConnectedModelInfo { BaseModel = "SDXL 1.0" },
            }
        );

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = sdxlTagged;

        Assert.IsFalse(vm.ShowWorkflowProfileWarning);
    }

    [TestMethod]
    public void AutoProfile_UnknownMetadataDoesNotSuppressFilenameWarning()
    {
        var vm = CreateViewModel();
        var mistagged = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = "z_image_turbo_bf16.safetensors",
                SharedFolderType = SharedFolderType.StableDiffusion,
                ConnectedModelInfo = new ConnectedModelInfo { BaseModel = "Other" },
            }
        );

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = mistagged;

        Assert.IsTrue(vm.ShowWorkflowProfileWarning);
        Assert.AreEqual("Move to DiffusionModels", vm.MoveModelToRecommendedFolderText);
    }

    [TestMethod]
    public void AutoProfile_UnetOnlyModelInCheckpointFolder_Warns()
    {
        var vm = CreateViewModel();
        // The classic support thread: a Z-Image (UNet-only) file dropped into the
        // StableDiffusion folder, silently loading as a checkpoint and failing.
        var zImage = LocalModel("z_image_turbo_bf16.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = zImage;

        Assert.IsTrue(vm.ShowWorkflowProfileWarning);
        StringAssert.Contains(vm.WorkflowProfileWarningText, "DiffusionModels");
    }

    [TestMethod]
    public void SwitchingFromUnetModelToCheckpoint_ClearsEncoderRequirement()
    {
        // Repro of the "No text encoders configured" report: selecting a UNet model turns
        // IsClipModelSelectionEnabled on, then picking an all-in-one checkpoint (e.g. Anima
        // AIO in StableDiffusion) must turn it back off - the encoder UI is hidden for
        // checkpoints, so a stale true makes generation demand encoders the user can't set.
        var vm = CreateViewModel();

        vm.SelectedUnifiedModel = LocalModel(
            "z_image_turbo_bf16.safetensors",
            SharedFolderType.DiffusionModels
        );
        Assert.IsTrue(vm.IsClipModelSelectionEnabled);

        vm.SelectedUnifiedModel = LocalModel("anima-aio-v1.0.safetensors", SharedFolderType.StableDiffusion);

        Assert.AreEqual(ModelLoader.Default, vm.SelectedModelLoader);
        Assert.IsFalse(vm.IsClipModelSelectionEnabled);
    }

    [TestMethod]
    public void SwitchingFromUnetModelToCheckpoint_ClearsAutoFilledVae()
    {
        var ae = LocalModel("ae.safetensors", SharedFolderType.VAE);
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var vm = CreateViewModel(clipModels: [qwen4B], vaeModels: [ae]);

        // Z-Image UNet auto-fills the Flux.1 VAE...
        vm.SelectedUnifiedModel = LocalModel(
            "z_image_turbo_bf16.safetensors",
            SharedFolderType.DiffusionModels
        );
        Assert.AreEqual(ae, vm.SelectedVae);

        // ...which must not stick around as a VAE override on an all-in-one checkpoint.
        vm.SelectedUnifiedModel = LocalModel("anima-aio-v1.0.safetensors", SharedFolderType.StableDiffusion);

        Assert.IsTrue(vm.SelectedVae?.IsDefault ?? false, "Auto-filled VAE should reset to Default");
    }

    [TestMethod]
    public void SelectingDefaultCheckpointProfile_WithLoaderAlreadyDefault_ClearsEncoderRequirement()
    {
        // Second half of the report: with the loader already on Default but the encoder flag
        // stuck on, picking "Default / Checkpoint" must clear it even though no loader flip
        // is needed.
        var vm = CreateViewModel();

        vm.SelectedUnifiedModel = LocalModel(
            "z_image_turbo_bf16.safetensors",
            SharedFolderType.DiffusionModels
        );
        vm.SelectedModelLoader = ModelLoader.Default;
        vm.IsClipModelSelectionEnabled = true;

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.DefaultCheckpoint;

        Assert.IsFalse(vm.IsClipModelSelectionEnabled);
    }

    [TestMethod]
    public void TogglingProfiles_OnUnetFolderModel_EncoderSectionAlwaysComesBack()
    {
        // Regression: picking "Default / Checkpoint" on a UNet-folder model cleared the
        // encoder flag while the folder guard kept the loader on UNet — and since the loader
        // never changed again, no amount of toggling brought the encoder slots back.
        var vm = CreateViewModel();
        var anima = LocalModel("anima-base-v1.0.safetensors", SharedFolderType.DiffusionModels);

        vm.SelectedUnifiedModel = anima;
        Assert.IsTrue(vm.ShowEncoderSection);

        // Toggle through checkpoint and back a few times, like a confused user would.
        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.DefaultCheckpoint;
        Assert.AreEqual(ModelLoader.Unet, vm.SelectedModelLoader, "Folder guard must keep the loader");
        Assert.IsTrue(
            vm.ShowEncoderSection,
            "Encoder slots must stay visible - the UNet loader still needs them at generation"
        );

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Anima;
        Assert.IsTrue(vm.ShowEncoderSection);

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.DefaultCheckpoint;
        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Auto;
        Assert.IsTrue(vm.ShowEncoderSection, "Auto on a UNet-folder model must restore the encoder UI");
    }

    [TestMethod]
    public void StandaloneProfile_WithStuckDisabledEncoderFlag_ReenablesEncoderSection()
    {
        // The re-enable direction: loader already UNet but the encoder flag was cleared
        // (e.g. by older builds with the one-way clear). Picking any standalone profile
        // must bring the encoder UI back.
        var vm = CreateViewModel();
        var anima = LocalModel("anima-base-v1.0.safetensors", SharedFolderType.DiffusionModels);

        vm.SelectedUnifiedModel = anima;
        vm.IsClipModelSelectionEnabled = false;
        Assert.IsFalse(vm.ShowEncoderSection);

        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.Anima;

        Assert.IsTrue(vm.IsClipModelSelectionEnabled);
        Assert.IsTrue(vm.ShowEncoderSection);
    }

    [TestMethod]
    public void DismissedWarning_StaysDismissedForSameModel_ReappearsForOthers()
    {
        var vm = CreateViewModel();
        var zImage = LocalModel("z_image_turbo_bf16.safetensors", SharedFolderType.StableDiffusion);
        var klein = LocalModel("flux2-klein-4b.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = zImage;
        Assert.IsTrue(vm.ShowWorkflowProfileWarning);

        // User clicks the dismiss button on the warning strip
        vm.DismissWorkflowProfileWarningCommand.Execute(null);
        Assert.IsFalse(vm.ShowWorkflowProfileWarning, "Dismissal should stick for the same model");

        // A different mismatched model must warn again
        vm.SelectedModel = klein;
        Assert.IsTrue(vm.ShowWorkflowProfileWarning, "A different model should re-show the warning");

        // Going back to the dismissed model stays quiet
        vm.SelectedModel = zImage;
        Assert.IsFalse(vm.ShowWorkflowProfileWarning);
    }

    [TestMethod]
    public void FolderMismatch_OffersMoveToDiffusionModels()
    {
        var vm = CreateViewModel();
        var zImage = LocalModel("z_image_turbo_bf16.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = zImage;

        Assert.AreEqual("Move to DiffusionModels", vm.MoveModelToRecommendedFolderText);
    }

    [TestMethod]
    public void FolderMismatch_OffersMoveToStableDiffusion()
    {
        var vm = CreateViewModel();
        var unetFile = LocalModel("some-model.safetensors", SharedFolderType.DiffusionModels);

        vm.SelectedModelLoader = ModelLoader.Unet;
        vm.SelectedUnetModel = unetFile;
        vm.SelectedWorkflowProfile = InferenceWorkflowProfile.DefaultCheckpoint;

        Assert.AreEqual("Move to StableDiffusion", vm.MoveModelToRecommendedFolderText);
    }

    [TestMethod]
    public void NoMismatch_NoMoveButtonText()
    {
        var vm = CreateViewModel();
        var checkpoint = LocalModel("dreamshaper.safetensors", SharedFolderType.StableDiffusion);

        vm.SelectedModelLoader = ModelLoader.Default;
        vm.SelectedModel = checkpoint;

        Assert.IsNull(vm.MoveModelToRecommendedFolderText);
    }

    [TestMethod]
    public void FindMovedModel_DuplicateFilename_SelectsExactDestinationPath()
    {
        var nested = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = Path.Combine("DiffusionModels", "archive", "model.safetensors"),
                SharedFolderType = SharedFolderType.DiffusionModels,
            }
        );
        var moved = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = Path.Combine("DiffusionModels", "model.safetensors"),
                SharedFolderType = SharedFolderType.DiffusionModels,
            }
        );

        var selected = ModelCardViewModel.FindMovedModel([nested, moved], "model.safetensors");

        // HybridModelFile.RelativePath is relative to DiffusionModels, unlike
        // LocalModelFile.RelativePath which includes the shared-folder prefix.
        Assert.AreEqual("model.safetensors", moved.RelativePath);
        Assert.AreSame(moved, selected);
    }

    [TestMethod]
    public void AutoSelect_KleinUnet_FillsMatchingEncoderAndVae()
    {
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var qwen8B = LocalModel("qwen_3_8b.safetensors", SharedFolderType.TextEncoders);
        var flux2Vae = LocalModel("flux2-vae.safetensors", SharedFolderType.VAE);

        var vm = CreateViewModel(clipModels: [qwen8B, qwen4B], vaeModels: [flux2Vae]);

        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);

        Assert.AreEqual(InferenceWorkflowProfile.Flux2, vm.ResolvedWorkflowProfile);
        Assert.AreEqual(1, vm.TextEncoders.Count);
        Assert.AreEqual(qwen4B, vm.TextEncoders[0].SelectedModel, "4B UNET must pair with qwen_3_4b");
        Assert.AreEqual(flux2Vae, vm.SelectedVae);
    }

    [TestMethod]
    public void AutoSelect_SwitchingKleinVariant_ReplacesAutoFilledEncoder()
    {
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var qwen8B = LocalModel("qwen_3_8b.safetensors", SharedFolderType.TextEncoders);

        var vm = CreateViewModel(clipModels: [qwen4B, qwen8B]);

        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreEqual(qwen4B, vm.TextEncoders[0].SelectedModel);

        // Swapping to the 9B UNET must replace OUR earlier 4B pick with the 8B encoder -
        // the mismatched pairing fails at sampling with a tensor-shape error.
        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-9b.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreEqual(qwen8B, vm.TextEncoders[0].SelectedModel);
    }

    [TestMethod]
    public void AutoSelect_DoesNotOverrideUserEncoderPick()
    {
        var qwen4B = LocalModel("qwen_3_4b.safetensors", SharedFolderType.TextEncoders);
        var customEncoder = LocalModel("my_custom_encoder.safetensors", SharedFolderType.TextEncoders);

        var vm = CreateViewModel(clipModels: [qwen4B, customEncoder]);

        // User picks the model, auto-fill runs, then the user overrides the encoder manually.
        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-4b.safetensors", SharedFolderType.DiffusionModels);
        vm.TextEncoders[0].SelectedModel = customEncoder;

        // A later model change must not stomp the user's explicit choice.
        vm.SelectedUnifiedModel = LocalModel("flux-2-klein-9b.safetensors", SharedFolderType.DiffusionModels);

        Assert.AreEqual(customEncoder, vm.TextEncoders[0].SelectedModel);
    }
}
