using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class VideoModelCardLightningPromptTests
{
    [TestInitialize]
    public void Initialize()
    {
        // ExtraNetworkCardViewModel observes on the current SynchronizationContext, which the
        // test runner doesn't provide.
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
    }

    private static (
        VideoModelCardViewModel Vm,
        TabContext TabContext,
        IServiceManager<ViewModelBase> Factory
    ) Create()
    {
        var clientManager = Substitute.For<IInferenceClientManager>();
        clientManager.UnetModels.Returns(new ObservableCollectionExtended<HybridModelFile>());
        clientManager.VaeModels.Returns(new ObservableCollectionExtended<HybridModelFile>());
        clientManager.ClipModels.Returns(new ObservableCollectionExtended<HybridModelFile>());
        var settingsManager = Substitute.For<ISettingsManager>();
        settingsManager.Settings.Returns(new Settings());

        IServiceManager<ViewModelBase> vmFactory = null!;
        vmFactory = new ServiceManager<ViewModelBase>(Substitute.For<IServiceProvider>())
            .Register(() => new ExtraNetworkCardViewModel(clientManager, settingsManager, vmFactory))
            .Register(() => new LoraModule(vmFactory));

        var tabContext = new TabContext();
        var vm = new VideoModelCardViewModel(clientManager, vmFactory, tabContext);
        return (vm, tabContext, vmFactory);
    }

    private static LoraModule EnabledLora(IServiceManager<ViewModelBase> vmFactory, string fileName)
    {
        var module = vmFactory.Get<LoraModule>();
        module.IsEnabled = true;
        module.GetCard<ExtraNetworkCardViewModel>().SelectedModel = HybridModelFile.FromLocal(
            new LocalModelFile { RelativePath = fileName, SharedFolderType = SharedFolderType.Lora }
        );
        return module;
    }

    /// <summary>JT's psychedelic-wolf setup: 4-step Seko LoRA enabled while CFG is 5.</summary>
    [TestMethod]
    public void LightningLoraAtHighCfg_ShowsPrompt_AndApplyReportsSteps()
    {
        var (vm, tabContext, vmFactory) = Create();
        tabContext.SamplerCfgScale = 5;

        vm.ExtraNetworksStackCardViewModel.AddCards(
            EnabledLora(vmFactory, "Wan2.2-T2V-A14B-4steps-lora-rank64-Seko-V1.safetensors")
        );

        Assert.IsTrue(vm.ShowLightningCfgPrompt);
        StringAssert.Contains(vm.LightningCfgPromptText, "4-step");

        int? requestedSteps = null;
        vm.LightningDefaultsRequested += steps => requestedSteps = steps;
        vm.ApplyLightningDefaultsCommand.Execute(null);
        Assert.AreEqual(4, requestedSteps);
    }

    [TestMethod]
    public void PromptHides_AtCfg1_AndWhenLoraDisabled()
    {
        var (vm, tabContext, vmFactory) = Create();
        tabContext.SamplerCfgScale = 5;

        var lora = EnabledLora(vmFactory, "minimax_h3_fl2v_turbo_8step_v1.0_comfyui_bf16.safetensors");
        vm.LowNoiseExtraNetworksStackCardViewModel.AddCards(lora);
        Assert.IsTrue(vm.ShowLightningCfgPrompt, "low-noise stack should be scanned too");

        tabContext.SamplerCfgScale = 1;
        Assert.IsFalse(vm.ShowLightningCfgPrompt, "CFG 1 needs no nudge");

        tabContext.SamplerCfgScale = 5;
        lora.IsEnabled = false;
        Assert.IsFalse(vm.ShowLightningCfgPrompt, "disabled LoRAs don't sample");
    }

    [TestMethod]
    public void OrdinaryLora_NeverPrompts()
    {
        var (vm, tabContext, vmFactory) = Create();
        tabContext.SamplerCfgScale = 5;

        vm.ExtraNetworksStackCardViewModel.AddCards(
            EnabledLora(vmFactory, "my_character_lora_v2.safetensors")
        );

        Assert.IsFalse(vm.ShowLightningCfgPrompt);
    }

    [TestMethod]
    public void Dismissal_ReArms_WhenLoraSetChanges()
    {
        var (vm, tabContext, vmFactory) = Create();
        tabContext.SamplerCfgScale = 5;

        vm.ExtraNetworksStackCardViewModel.AddCards(
            EnabledLora(vmFactory, "wan21_lightx2v_cfg_step_distill_lora.safetensors")
        );
        Assert.IsTrue(vm.ShowLightningCfgPrompt);

        vm.DismissLightningPromptCommand.Execute(null);
        Assert.IsFalse(vm.ShowLightningCfgPrompt);

        vm.ExtraNetworksStackCardViewModel.AddCards(
            EnabledLora(vmFactory, "sdxl_lightning_lora.safetensors")
        );
        Assert.IsTrue(vm.ShowLightningCfgPrompt, "changing the LoRA set re-arms the prompt");
    }
}
