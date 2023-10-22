using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using DynamicData.Binding;
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
using Path = System.IO.Path;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceImageUpscaleView), persistent: true)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
[ManagedService]
[Transient]
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
        ServiceManager<ViewModelBase> vmFactory
    )
        : base(vmFactory, inferenceClientManager, notificationService, settingsManager)
    {
        this.notificationService = notificationService;

        UpscalerCardViewModel = vmFactory.Get<UpscalerCardViewModel>();
        SharpenCardViewModel = vmFactory.Get<SharpenCardViewModel>();
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();
        StackCardViewModel.AddCards(
            new LoadableViewModelBase[]
            {
                // Upscaler
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Upscale";
                    stackExpander.AddCards(new LoadableViewModelBase[] { UpscalerCardViewModel });
                }),
                // Sharpen
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Sharpen";
                    stackExpander.AddCards(new LoadableViewModelBase[] { SharpenCardViewModel });
                })
            }
        );

        // On any new images, copy to input dir
        SelectImageCardViewModel
            .WhenPropertyChanged(x => x.ImageSource)
            .Subscribe(e =>
            {
                if (e.Value?.LocalFile?.FullPath is { } path)
                {
                    ClientManager.CopyImageToInputAsync(path).SafeFireAndForget();
                }
            });
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        base.BuildPrompt(args);

        var builder = args.Builder;
        var nodes = builder.Nodes;

        // Get source image
        var sourceImage = SelectImageCardViewModel.ImageSource;
        var sourceImageRelativePath = Path.Combine("Inference", sourceImage!.LocalFile!.Name);
        var sourceImageSize =
            SelectImageCardViewModel.CurrentBitmapSize
            ?? throw new InvalidOperationException("Source image size is null");

        // Set source size
        builder.Connections.PrimarySize = sourceImageSize;

        // Load source
        var loadImage = nodes.AddNamedNode(
            ComfyNodeBuilder.LoadImage("LoadImage", sourceImageRelativePath)
        );
        builder.Connections.Primary = loadImage.Output1;

        // If upscale is enabled, add another upscale group
        if (IsUpscaleEnabled)
        {
            var upscaleSize = builder.Connections.PrimarySize.WithScale(
                UpscalerCardViewModel.Scale
            );

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

        await ClientManager.CopyImageToInputAsync(path, cancellationToken);

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
