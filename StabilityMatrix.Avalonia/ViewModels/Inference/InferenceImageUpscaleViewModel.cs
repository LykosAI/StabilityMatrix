using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceImageUpscaleView), persistent: true)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
[ManagedService]
[RegisterTransient<InferenceImageUpscaleViewModel>]
public class InferenceImageUpscaleViewModel : InferenceGenerationViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;

    [JsonIgnore]
    public StackCardViewModel StackCardViewModel { get; }

    [JsonPropertyName("Upscaler")]
    public UpscalerCardViewModel UpscalerCardViewModel { get; }

    [JsonPropertyName("Sharpen")]
    public SharpenCardViewModel SharpenCardViewModel { get; }

    [JsonPropertyName("SelectImage")]
    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    public bool IsUpscaleEnabled
    {
        get => StackCardViewModel.GetCard<StackExpanderViewModel>().IsEnabled;
        set => StackCardViewModel.GetCard<StackExpanderViewModel>().IsEnabled = value;
    }

    public bool IsSharpenEnabled
    {
        get => StackCardViewModel.GetCard<StackExpanderViewModel>(1).IsEnabled;
        set => StackCardViewModel.GetCard<StackExpanderViewModel>(1).IsEnabled = value;
    }

    public InferenceImageUpscaleViewModel(
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> vmFactory,
        RunningPackageService runningPackageService
    )
        : base(vmFactory, inferenceClientManager, notificationService, settingsManager, runningPackageService)
    {
        this.notificationService = notificationService;

        UpscalerCardViewModel = vmFactory.Get<UpscalerCardViewModel>();
        SharpenCardViewModel = vmFactory.Get<SharpenCardViewModel>();
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();
        StackCardViewModel.AddCards(
            vmFactory.Get<StackExpanderViewModel>(stackExpander =>
            {
                stackExpander.Title = "Upscale";
                stackExpander.AddCards(UpscalerCardViewModel);
            }),
            vmFactory.Get<StackExpanderViewModel>(stackExpander =>
            {
                stackExpander.Title = "Sharpen";
                stackExpander.AddCards(SharpenCardViewModel);
            })
        );
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        if (SelectImageCardViewModel.ImageSource is { } imageSource)
        {
            yield return imageSource;
        }
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        base.BuildPrompt(args);

        var builder = args.Builder;
        var nodes = builder.Nodes;

        // Setup image source
        SelectImageCardViewModel.ApplyStep(args);

        // If upscale is enabled, add another upscale group
        if (IsUpscaleEnabled)
        {
            var upscaleSize = builder.Connections.PrimarySize.WithScale(UpscalerCardViewModel.Scale);

            // Build group
            builder.Connections.Primary = builder
                .Group_UpscaleToImage(
                    "Upscale",
                    builder.GetPrimaryAsImage(),
                    UpscalerCardViewModel.SelectedUpscaler!.Value,
                    upscaleSize.Width,
                    upscaleSize.Height
                )
                .Output;
        }

        // If sharpen is enabled, add another sharpen group
        if (IsSharpenEnabled)
        {
            builder.Connections.Primary = nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.ImageSharpen
                    {
                        Name = "Sharpen",
                        Image = builder.GetPrimaryAsImage(),
                        SharpenRadius = SharpenCardViewModel.SharpenRadius,
                        Sigma = SharpenCardViewModel.Sigma,
                        Alpha = SharpenCardViewModel.Alpha
                    }
                )
                .Output;
        }

        builder.SetupOutputImage();
    }

    /// <inheritdoc />
    protected override async Task GenerateImageImpl(
        GenerateOverrides overrides,
        CancellationToken cancellationToken
    )
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show("Client not connected", "Please connect first");
            return;
        }

        if (SelectImageCardViewModel.ImageSource?.LocalFile?.FullPath is not { } path)
        {
            notificationService.Show("No image selected", "Please select an image first");
            return;
        }

        foreach (var image in GetInputImages())
        {
            await ClientManager.UploadInputImageAsync(image, cancellationToken);
        }

        var buildPromptArgs = new BuildPromptEventArgs { Overrides = overrides };
        BuildPrompt(buildPromptArgs);

        var generationArgs = new ImageGenerationEventArgs
        {
            Client = ClientManager.Client,
            Nodes = buildPromptArgs.Builder.ToNodeDictionary(),
            OutputNodeNames = buildPromptArgs.Builder.Connections.OutputNodeNames.ToArray(),
            Parameters = new GenerationParameters
            {
                ModelName = UpscalerCardViewModel.SelectedUpscaler?.Name,
            },
            Project = InferenceProjectDocument.FromLoadable(this)
        };

        await RunGeneration(generationArgs, cancellationToken);
    }
}
