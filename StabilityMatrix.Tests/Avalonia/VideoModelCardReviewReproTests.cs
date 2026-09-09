using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class VideoModelCardReviewReproTests
{
    [TestMethod]
    public void DecoderStartingAfterDisposal_DoesNotThrowOutsideWorkerHandler()
    {
        // Model the legal schedule where Dispose runs before the newly started thread
        // first enters ReaderLoop. No ffmpeg process or bitmap is needed for this path.
        var type = typeof(global::Avalonia.Gif.VideoInstance);
        var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        cts.Dispose();
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        type.GetField("<CurrentCts>k__BackingField", flags)!.SetValue(instance, cts);
        type.GetField("<IsDisposed>k__BackingField", flags)!.SetValue(instance, true);
        type.GetField("gate", flags)!.SetValue(instance, new object());
        type.GetMethod("ReaderLoop", flags)!.Invoke(instance, null);
    }

    private static HybridModelFile Local(string name, SharedFolderType type) =>
        HybridModelFile.FromLocal(new LocalModelFile { RelativePath = name, SharedFolderType = type });

    private static VideoModelCardViewModel Create(
        ObservableCollectionExtended<HybridModelFile> vaes,
        ObservableCollectionExtended<HybridModelFile> clips
    )
    {
        var client = Substitute.For<IInferenceClientManager>();
        client.VaeModels.Returns(vaes);
        client.ClipModels.Returns(clips);
        client.UnetModels.Returns(new ObservableCollectionExtended<HybridModelFile>());
        return new(client, Substitute.For<IServiceManager<ViewModelBase>>(), new TabContext());
    }

    [TestMethod]
    public void SwitchingWanToH3_ReplacesAutomaticallyPickedCompanions()
    {
        var wanVae = Local("wan_2.1_vae.safetensors", SharedFolderType.VAE);
        var h3Vae = Local("minimax_h3_video_vae_fp16.safetensors", SharedFolderType.VAE);
        var wanClip = Local("umt5_xxl.safetensors", SharedFolderType.TextEncoders);
        var h3Clip = Local("qwen3vl_32b_minimax_h3.safetensors", SharedFolderType.TextEncoders);
        var vm = Create(new([wanVae, h3Vae]), new([wanClip, h3Clip]));
        vm.SelectedModel = Local("wan2.1_t2v_14B.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreEqual(wanVae.RelativePath, vm.SelectedVae?.RelativePath);
        vm.SelectedModel = Local("minimax_h3.safetensors", SharedFolderType.DiffusionModels);
        Assert.AreEqual(VideoWorkflowProfile.MiniMaxH3, vm.ResolvedWorkflowProfile);
        Assert.AreEqual(
            h3Vae.RelativePath,
            vm.SelectedVae?.RelativePath,
            "H3 must not retain the auto-picked Wan VAE"
        );
        Assert.AreEqual(h3Clip.RelativePath, vm.SelectedClipModel?.RelativePath);
    }

    [TestMethod]
    public void SwitchingWan14BTo5B_ReplacesAutomaticallyPickedVae()
    {
        var oldVae = Local("wan_2.1_vae.safetensors", SharedFolderType.VAE);
        var newVae = Local("wan2.2_vae.safetensors", SharedFolderType.VAE);
        var vm = Create(new([oldVae, newVae]), new());
        vm.SelectedModel = Local("wan2.1_t2v_14B.safetensors", SharedFolderType.DiffusionModels);
        vm.SelectedModel = Local("wan2.2_ti2v_5B.safetensors", SharedFolderType.DiffusionModels);
        Assert.IsTrue(vm.UseWan22FiveBLatent);
        Assert.AreEqual(
            newVae.RelativePath,
            vm.SelectedVae?.RelativePath,
            "5B latent must not retain the auto-picked 2.1 VAE"
        );
    }

    [TestMethod]
    public void CompanionsArrivingAfterModelSelection_FillEmptySlots()
    {
        var vaes = new ObservableCollectionExtended<HybridModelFile>();
        var vm = Create(vaes, new());
        vm.SelectedModel = Local("minimax_h3.safetensors", SharedFolderType.DiffusionModels);
        var h3Vae = Local("minimax_h3_video_vae_fp16.safetensors", SharedFolderType.VAE);
        vaes.Add(h3Vae);
        Assert.AreEqual(
            h3Vae.RelativePath,
            vm.SelectedVae?.RelativePath,
            "Refreshing the library must retry an empty auto-fill slot"
        );
    }

    [TestMethod]
    public void ManualSelectionsSurviveModelSwitchAndLibraryRefresh()
    {
        var vaes = new ObservableCollectionExtended<HybridModelFile>();
        var vm = Create(vaes, new());
        vm.SelectedModel = Local("wan2.1.safetensors", SharedFolderType.DiffusionModels);
        vm.SelectedVae = Local("custom_vae.safetensors", SharedFolderType.VAE);
        vm.SelectedClipModel = Local("custom_encoder.safetensors", SharedFolderType.TextEncoders);
        var chosenVae = vm.SelectedVae.RelativePath;
        var chosenClip = vm.SelectedClipModel.RelativePath;
        vm.SelectedModel = Local("minimax_h3.safetensors", SharedFolderType.DiffusionModels);
        vaes.Add(Local("minimax_h3_video_vae_fp16.safetensors", SharedFolderType.VAE));
        Assert.AreEqual(chosenVae, vm.SelectedVae?.RelativePath);
        Assert.AreEqual(chosenClip, vm.SelectedClipModel?.RelativePath);
    }

    [TestMethod]
    public async Task H3ValidationIgnoresHiddenWanExpertSetting()
    {
        var vm = Create(new(), new());
        vm.SelectedModel = Local("minimax_h3.safetensors", SharedFolderType.DiffusionModels);
        vm.SelectedVae = Local("minimax_h3_video_vae_fp16.safetensors", SharedFolderType.VAE);
        vm.SelectedClipModel = Local("qwen3vl_32b_minimax_h3.safetensors", SharedFolderType.TextEncoders);
        vm.IsLowNoiseModelEnabled = true;
        Assert.IsFalse(vm.ShowMoeExpertControls);
        Assert.IsTrue(await vm.ValidateModel());
    }

    [TestMethod]
    public void SwitchingToFamilyWithoutCompanionsClearsOldAutoPicks()
    {
        var vm = Create(
            new([Local("wan_2.1_vae.safetensors", SharedFolderType.VAE)]),
            new([Local("umt5_xxl.safetensors", SharedFolderType.TextEncoders)])
        );
        vm.SelectedModel = Local("wan2.1.safetensors", SharedFolderType.DiffusionModels);
        Assert.IsNotNull(vm.SelectedVae);
        Assert.IsNotNull(vm.SelectedClipModel);
        vm.SelectedModel = Local("minimax_h3.safetensors", SharedFolderType.DiffusionModels);
        Assert.IsNull(vm.SelectedVae);
        Assert.IsNull(vm.SelectedClipModel);
    }
}
