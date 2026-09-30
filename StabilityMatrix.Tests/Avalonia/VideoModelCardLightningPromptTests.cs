using DynamicData.Binding;
using NSubstitute;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
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

    private static LoraModule EnabledLora(
        IServiceManager<ViewModelBase> vmFactory,
        string fileName,
        ConnectedModelInfo? connectedModelInfo = null
    )
    {
        var module = vmFactory.Get<LoraModule>();
        module.IsEnabled = true;
        module.GetCard<ExtraNetworkCardViewModel>().SelectedModel = HybridModelFile.FromLocal(
            new LocalModelFile
            {
                RelativePath = fileName,
                SharedFolderType = SharedFolderType.Lora,
                ConnectedModelInfo = connectedModelInfo,
            }
        );
        return module;
    }

    [TestMethod]
    public void LoraModules_OnlyEnabledAndSelectedModelsReachOutputFilename()
    {
        var (_, _, factory) = Create();
        var builder = new ComfyNodeBuilder();
        builder.Connections.Base.Model = new ModelNodeConnection { Data = ["Checkpoint", 0] };
        builder.Connections.Base.Clip = new ClipNodeConnection { Data = ["Checkpoint", 1] };
        var enabled = EnabledLora(factory, "Lora/sub/style.safetensors");
        var disabled = EnabledLora(factory, "Lora/disabled.safetensors");
        disabled.IsEnabled = false;
        var unselected = factory.Get<LoraModule>();
        unselected.IsEnabled = true;
        var args = new ModuleApplyStepEventArgs { Builder = builder };
        enabled.ApplyStep(args);
        disabled.ApplyStep(args);
        unselected.ApplyStep(args);
        var provider = new FileNameFormatProvider
        {
            GenerationLoras = GenerationLoraSnapshot.FromNodes(builder.Nodes.Values),
        };
        Assert.AreEqual("style", FileNameFormat.Parse("{lora_name}", provider).GetFileName());
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

    /// <summary>
    /// Renamed local files often only carry the step count in their CivitAI metadata
    /// ("i2v_high_noise_model.safetensors" whose connected name is the Seko 4-step LoRA).
    /// </summary>
    [TestMethod]
    public void RenamedLora_DetectedViaConnectedModelName()
    {
        var (vm, tabContext, vmFactory) = Create();
        tabContext.SamplerCfgScale = 5;

        vm.ExtraNetworksStackCardViewModel.AddCards(
            EnabledLora(
                vmFactory,
                "i2v_high_noise_model.safetensors",
                new ConnectedModelInfo
                {
                    ModelName = "Wan2.2-I2V-A14B-4steps-lora-rank64-Seko-V1",
                    VersionName = "Seko_lora_high noise",
                }
            )
        );

        Assert.IsTrue(vm.ShowLightningCfgPrompt);
        StringAssert.Contains(vm.LightningCfgPromptText, "4-step");
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
