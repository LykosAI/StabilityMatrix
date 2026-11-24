using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Inference;
using Size = System.Drawing.Size;

namespace StabilityMatrix.Tests.Avalonia;

/// <summary>
/// One project in the saved-project corpus: a tab type and the settings applied to a new tab of it.
/// Every setting is one 2.16.5 can save, so the same scenario can be written by any version since.
/// </summary>
internal sealed record InferenceProjectScenario(
    string Name,
    InferenceProjectType ProjectType,
    Action<InferenceTabViewModelBase, InferenceTestHost> Apply
)
{
    public const string Prompt = "a white wolf in a snowy forest, <lora:detail_tweaker_xl:0.8>, detailed fur";
    public const string NegativePrompt = "blurry, lowres";
    public const long Seed = 424242;

    public static IReadOnlyList<InferenceProjectScenario> All { get; } =
    [new("text-to-image-sdxl", InferenceProjectType.TextToImage, (tab, host) =>
        {
            var vm = (InferenceTextToImageViewModel)tab;
            vm.ModelCardViewModel.SelectedModel = host.Model("sd_xl_base_1.0.safetensors");
            vm.ModelCardViewModel.IsVaeSelectionEnabled = true;
            vm.ModelCardViewModel.SelectedVae = host.Model("sdxl_vae.safetensors");
            Common(vm.PromptCardViewModel, vm.SeedCardViewModel, vm.SamplerCardViewModel);
            vm.SamplerCardViewModel.Width = 832;
            vm.SamplerCardViewModel.Height = 1216;
            vm.BatchSizeCardViewModel.BatchSize = 2;
        }), new("text-to-image-flux2", InferenceProjectType.TextToImage, (tab, host) =>
        {
            var vm = (InferenceTextToImageViewModel)tab;
            vm.ModelCardViewModel.SelectedModelLoader = ModelLoader.Unet;
            // A name Auto detection doesn't recognize, so only the saved profile makes this Flux.2
            vm.ModelCardViewModel.SelectedUnetModel = host.Model("custom_unet_v3.safetensors");
            vm.ModelCardViewModel.SelectedWorkflowProfile = InferenceWorkflowProfile.Flux2;
            vm.ModelCardViewModel.TextEncoders[0].SelectedModel = host.Model(
                "mistral_3_small_flux2_fp8.safetensors"
            );
            vm.ModelCardViewModel.SelectedVae = host.Model("flux2-vae.safetensors");
            Common(vm.PromptCardViewModel, vm.SeedCardViewModel, vm.SamplerCardViewModel);
            vm.SamplerCardViewModel.CfgScale = 4;
        }), new("image-to-image-sdxl", InferenceProjectType.ImageToImage, (tab, host) =>
        {
            var vm = (InferenceImageToImageViewModel)tab;
            vm.ModelCardViewModel.SelectedModel = host.Model("sd_xl_base_1.0.safetensors");
            Common(vm.PromptCardViewModel, vm.SeedCardViewModel, vm.SamplerCardViewModel);
            vm.SamplerCardViewModel.DenoiseStrength = 0.55;
        }), new("upscale", InferenceProjectType.Upscale, (tab, _) =>
        {
            var vm = (InferenceImageUpscaleViewModel)tab;
            vm.UpscalerCardViewModel.SelectedUpscaler = new ComfyUpscaler(
                "4x-UltraSharp.pth",
                ComfyUpscalerType.ESRGAN
            );
            vm.UpscalerCardViewModel.Scale = 2;
        }), new("flux-text-to-image", InferenceProjectType.FluxTextToImage, (tab, host) =>
        {
            var vm = (InferenceFluxTextToImageViewModel)tab;
            vm.ModelCardViewModel.SelectedModel = host.Model("flux1-dev.safetensors");
            vm.ModelCardViewModel.SelectedClip1 = host.Model("t5xxl_fp16.safetensors");
            vm.ModelCardViewModel.SelectedClip2 = host.Model("clip_l.safetensors");
            vm.ModelCardViewModel.SelectedVae = host.Model("ae.safetensors");
            Common(vm.PromptCardViewModel, vm.SeedCardViewModel, vm.SamplerCardViewModel);
        }), new("wan-text-to-video", InferenceProjectType.WanTextToVideo, (tab, host) =>
        {
            var vm = (InferenceTextToVideoViewModel)tab;
            vm.ModelCardViewModel.SelectedModel = host.Model("wan2.1_t2v_1.3B_fp16.safetensors");
            vm.ModelCardViewModel.SelectedClipModel = host.Model("umt5_xxl_fp8_e4m3fn_scaled.safetensors");
            vm.ModelCardViewModel.SelectedVae = host.Model("wan_2.1_vae.safetensors");
            vm.ModelCardViewModel.Shift = 8;
            Common(vm.PromptCardViewModel, vm.SeedCardViewModel, vm.SamplerCardViewModel);
            vm.SamplerCardViewModel.Length = 49;
            vm.VideoOutputSettingsCardViewModel.Fps = 16;
        }), new("wan-image-to-video", InferenceProjectType.WanImageToVideo, (tab, host) =>
        {
            var vm = (InferenceImageToVideoViewModel)tab;
            vm.ModelCardViewModel.SelectedModel = host.Model("wan2.1_i2v_480p_14B_fp16.safetensors");
            vm.ModelCardViewModel.SelectedClipModel = host.Model("umt5_xxl_fp8_e4m3fn_scaled.safetensors");
            vm.ModelCardViewModel.SelectedVae = host.Model("wan_2.1_vae.safetensors");
            vm.ModelCardViewModel.IsClipVisionEnabled = true;
            vm.ModelCardViewModel.SelectedClipVisionModel = host.Model("clip_vision_h.safetensors");
            Common(vm.PromptCardViewModel, vm.SeedCardViewModel, vm.SamplerCardViewModel);
            vm.SamplerCardViewModel.Length = 33;
        }), new("svd-image-to-video", InferenceProjectType.ImageToVideo, (tab, host) =>
        {
            var vm = (InferenceSvdImageToVideoViewModel)tab;
            vm.ModelCardViewModel.SelectedModel = host.Model("svd_xt_1_1.safetensors");
            vm.SeedCardViewModel.Seed = Seed;
            vm.SamplerCardViewModel.Steps = 25;
            vm.SvdImgToVidConditioningViewModel.MotionBucketId = 150;
            vm.SvdImgToVidConditioningViewModel.Fps = 8;
        })];

    private static void Common(
        PromptCardViewModel prompt,
        SeedCardViewModel seed,
        SamplerCardViewModel sampler
    )
    {
        prompt.PromptDocument.Text = Prompt;
        prompt.NegativePromptDocument.Text = NegativePrompt;
        seed.Seed = Seed;
        sampler.Steps = 23;
        sampler.CfgScale = 6.5;
        sampler.SelectedSampler = new ComfySampler("dpmpp_2m");
        sampler.SelectedScheduler = new ComfyScheduler("karras");
    }

    /// <summary>
    /// Points a tab's input image at <see cref="InferenceTestHost.InputImagePath"/>. Saved projects
    /// carry the path of the machine that wrote them, so this applies after loading too.
    /// </summary>
    public static void UseInputImage(InferenceTabViewModelBase tab, InferenceTestHost host)
    {
        var card = tab switch
        {
            InferenceImageToImageViewModel vm => vm.SelectImageCardViewModel,
            InferenceImageUpscaleViewModel vm => vm.SelectImageCardViewModel,
            InferenceImageToVideoViewModel vm => vm.SelectImageCardViewModel,
            InferenceSvdImageToVideoViewModel vm => vm.SelectImageCardViewModel,
            _ => null,
        };
        if (card is null)
        {
            return;
        }

        var image = new ImageSource(new FilePath(host.InputImagePath));
        // Building the prompt reads the image's cached hash
        image.GetBlake3HashAsync().GetAwaiter().GetResult();
        card.ImageSource = image;
        card.CurrentBitmapSize = new Size(1, 1);
    }
}
