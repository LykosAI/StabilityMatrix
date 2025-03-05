using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceWanImageToVideoView), IsPersistent = true)]
[RegisterTransient<InferenceWanImageToVideoViewModel>, ManagedService]
public class InferenceWanImageToVideoViewModel : InferenceWanTextToVideoViewModel
{
    public InferenceWanImageToVideoViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager inferenceClientManager,
        INotificationService notificationService,
        ISettingsManager settingsManager,
        RunningPackageService runningPackageService
    )
        : base(vmFactory, inferenceClientManager, notificationService, settingsManager, runningPackageService)
    {
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        SamplerCardViewModel.IsDenoiseStrengthEnabled = true;
        SamplerCardViewModel.Width = 512;
        SamplerCardViewModel.Height = 512;

        ModelCardViewModel.IsClipVisionEnabled = true;
    }

    [JsonPropertyName("ImageLoader")]
    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        var builder = args.Builder;

        builder.Connections.Seed = args.SeedOverride switch
        {
            { } seed => Convert.ToUInt64(seed),
            _ => Convert.ToUInt64(SeedCardViewModel.Seed)
        };

        // Load models
        ModelCardViewModel.ApplyStep(args);

        // Setup latent from image
        var imageLoad = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage
            {
                Name = builder.Nodes.GetUniqueName("ControlNet_LoadImage"),
                Image =
                    SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached("Inference")
                    ?? throw new ValidationException()
            }
        );
        builder.Connections.Primary = imageLoad.Output1;
        builder.Connections.PrimarySize = SelectImageCardViewModel.CurrentBitmapSize;

        BatchSizeCardViewModel.ApplyStep(args);

        SelectImageCardViewModel.ApplyStep(args);

        PromptCardViewModel.ApplyStep(args);

        SamplerCardViewModel.ApplyStep(args);

        // Animated webp output
        VideoOutputSettingsCardViewModel.ApplyStep(args);
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        if (SelectImageCardViewModel.ImageSource is { } image)
        {
            yield return image;
        }
    }
}
