using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using NLog;
using Refit;
using SkiaSharp;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

/// <summary>
/// Abstract base class for tab view models that generate images using ClientManager.
/// This includes a progress reporter, image output view model, and generation virtual methods.
/// </summary>
[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public abstract partial class InferenceGenerationViewModelBase
    : InferenceTabViewModelBase,
        IImageGalleryComponent
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    [JsonPropertyName("ImageGallery")]
    public ImageGalleryCardViewModel ImageGalleryCardViewModel { get; }

    [JsonIgnore]
    public ImageFolderCardViewModel ImageFolderCardViewModel { get; }

    [JsonIgnore]
    public ProgressViewModel OutputProgress { get; } = new();

    [JsonIgnore]
    public IInferenceClientManager ClientManager { get; }

    /// <inheritdoc />
    protected InferenceGenerationViewModelBase(
        ServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager inferenceClientManager,
        INotificationService notificationService
    )
        : base(notificationService)
    {
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;

        ClientManager = inferenceClientManager;

        ImageGalleryCardViewModel = vmFactory.Get<ImageGalleryCardViewModel>();
        ImageFolderCardViewModel = vmFactory.Get<ImageFolderCardViewModel>();

        GenerateImageCommand.WithConditionalNotificationErrorHandler(notificationService);
    }

    /// <summary>
    /// Builds the image generation prompt
    /// </summary>
    protected virtual void BuildPrompt(BuildPromptEventArgs args) { }

    /// <summary>
    /// Runs a generation task
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if args.Parameters or args.Project are null</exception>
    protected async Task RunGeneration(
        ImageGenerationEventArgs args,
        CancellationToken cancellationToken
    )
    {
        var client = args.Client;
        var nodes = args.Nodes;

        // Checks
        if (args.Parameters is null)
            throw new InvalidOperationException("Parameters is null");
        if (args.Project is null)
            throw new InvalidOperationException("Project is null");
        if (args.OutputNodeNames.Count == 0)
            throw new InvalidOperationException("OutputNodeNames is empty");
        if (client.OutputImagesDir is null)
            throw new InvalidOperationException("OutputImagesDir is null");

        // Connect preview image handler
        client.PreviewImageReceived += OnPreviewImageReceived;

        // Register to interrupt if user cancels
        var promptInterrupt = cancellationToken.Register(() =>
        {
            Logger.Info("Cancelling prompt");
            client
                .InterruptPromptAsync(new CancellationTokenSource(5000).Token)
                .SafeFireAndForget(ex =>
                {
                    Logger.Warn(ex, "Error while interrupting prompt");
                });
        });

        ComfyTask? promptTask = null;

        try
        {
            var timer = Stopwatch.StartNew();

            try
            {
                promptTask = await client.QueuePromptAsync(nodes, cancellationToken);
            }
            catch (ApiException e)
            {
                Logger.Warn(e, "Api exception while queuing prompt");
                await DialogHelper.CreateApiExceptionDialog(e, "Api Error").ShowAsync();
                return;
            }

            // Register progress handler
            promptTask.ProgressUpdate += OnProgressUpdateReceived;

            // Delay attaching running node change handler to not show indeterminate progress
            // if progress updates are received before the prompt starts
            Task.Run(
                    async () =>
                    {
                        var delayTime = 250 - (int)timer.ElapsedMilliseconds;
                        if (delayTime > 0)
                        {
                            await Task.Delay(delayTime, cancellationToken);
                        }
                        // ReSharper disable once AccessToDisposedClosure
                        AttachRunningNodeChangedHandler(promptTask);
                    },
                    cancellationToken
                )
                .SafeFireAndForget();

            // Wait for prompt to finish
            await promptTask.Task.WaitAsync(cancellationToken);
            Logger.Trace($"Prompt task {promptTask.Id} finished");

            // Get output images
            var imageOutputs = await client.GetImagesForExecutedPromptAsync(
                promptTask.Id,
                cancellationToken
            );

            // Disable cancellation
            await promptInterrupt.DisposeAsync();

            ImageGalleryCardViewModel.ImageSources.Clear();

            if (
                !imageOutputs.TryGetValue(args.OutputNodeNames[0], out var images) || images is null
            )
            {
                // No images match
                notificationService.Show("No output", "Did not receive any output images");
                return;
            }

            await ProcessOutputImages(images, args);
        }
        finally
        {
            // Disconnect progress handler
            client.PreviewImageReceived -= OnPreviewImageReceived;

            // Clear progress
            OutputProgress.ClearProgress();
            ImageGalleryCardViewModel.PreviewImage?.Dispose();
            ImageGalleryCardViewModel.PreviewImage = null;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = false;

            // Cleanup tasks
            promptTask?.Dispose();
        }
    }

    /// <summary>
    /// Handles image output metadata for generation runs
    /// </summary>
    private async Task ProcessOutputImages(
        IReadOnlyCollection<ComfyImage> images,
        ImageGenerationEventArgs args
    )
    {
        // Write metadata to images
        var outputImages = new List<ImageSource>();
        foreach (
            var (i, filePath) in images
                .Select(image => image.ToFilePath(args.Client.OutputImagesDir!))
                .Enumerate()
        )
        {
            if (!filePath.Exists)
            {
                Logger.Warn($"Image file {filePath} does not exist");
                continue;
            }

            var parameters = args.Parameters!;
            var project = args.Project!;

            // Seed and batch override for batches
            if (images.Count > 1 && project.ProjectType is InferenceProjectType.TextToImage)
            {
                project = (InferenceProjectDocument)project.Clone();

                // Set batch size indexes
                project.TryUpdateModel(
                    "BatchSize",
                    node =>
                    {
                        node[nameof(BatchSizeCardViewModel.IsBatchIndexEnabled)] = true;
                        node[nameof(BatchSizeCardViewModel.BatchIndex)] = i + 1;
                        return node;
                    }
                );
            }

            var bytesWithMetadata = PngDataHelper.AddMetadata(
                await filePath.ReadAllBytesAsync(),
                parameters,
                project
            );

            await using (var outputStream = filePath.Info.OpenWrite())
            {
                await outputStream.WriteAsync(bytesWithMetadata);
                await outputStream.FlushAsync();
            }

            outputImages.Add(new ImageSource(filePath));

            EventManager.Instance.OnImageFileAdded(filePath);
        }

        // Download all images to make grid, if multiple
        if (outputImages.Count > 1)
        {
            var outputDir = outputImages[0].LocalFile!.Directory;

            var loadedImages = outputImages
                .Select(i => i.LocalFile)
                .Where(f => f is { Exists: true })
                .Select(f =>
                {
                    using var stream = f!.Info.OpenRead();
                    return SKImage.FromEncodedData(stream);
                })
                .ToImmutableArray();

            var grid = ImageProcessor.CreateImageGrid(loadedImages);
            var gridBytes = grid.Encode().ToArray();
            var gridBytesWithMetadata = PngDataHelper.AddMetadata(
                gridBytes,
                args.Parameters!,
                args.Project!
            );

            // Save to disk
            var lastName = outputImages.Last().LocalFile?.Info.Name;
            var gridPath = outputDir!.JoinFile($"grid-{lastName}");

            await using (var fileStream = gridPath.Info.OpenWrite())
            {
                await fileStream.WriteAsync(gridBytesWithMetadata);
            }

            // Insert to start of images
            var gridImage = new ImageSource(gridPath);
            // Preload
            await gridImage.GetBitmapAsync();
            ImageGalleryCardViewModel.ImageSources.Add(gridImage);

            EventManager.Instance.OnImageFileAdded(gridPath);
        }

        // Add rest of images
        foreach (var img in outputImages)
        {
            // Preload
            await img.GetBitmapAsync();
            ImageGalleryCardViewModel.ImageSources.Add(img);
        }
    }

    /// <summary>
    /// Implementation for Generate Image
    /// </summary>
    protected virtual Task GenerateImageImpl(
        GenerateOverrides overrides,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Command for the Generate Image button
    /// </summary>
    /// <param name="options">Optional overrides (side buttons)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [RelayCommand(IncludeCancelCommand = true, FlowExceptionsToTaskScheduler = true)]
    private async Task GenerateImage(
        GenerateFlags options = default,
        CancellationToken cancellationToken = default
    )
    {
        var overrides = GenerateOverrides.FromFlags(options);

        try
        {
            await GenerateImageImpl(overrides, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug($"Image Generation Canceled");
        }
    }

    /// <summary>
    /// Shows a prompt and return false if client not connected
    /// </summary>
    protected async Task<bool> CheckClientConnectedWithPrompt()
    {
        if (ClientManager.IsConnected)
            return true;

        var vm = vmFactory.Get<InferenceConnectionHelpViewModel>();
        await vm.CreateDialog().ShowAsync();

        return ClientManager.IsConnected;
    }

    /// <summary>
    /// Handles the preview image received event from the websocket.
    /// Updates the preview image in the image gallery.
    /// </summary>
    protected virtual void OnPreviewImageReceived(object? sender, ComfyWebSocketImageData args)
    {
        ImageGalleryCardViewModel.SetPreviewImage(args.ImageBytes);
    }

    /// <summary>
    /// Handles the progress update received event from the websocket.
    /// Updates the progress view model.
    /// </summary>
    protected virtual void OnProgressUpdateReceived(
        object? sender,
        ComfyProgressUpdateEventArgs args
    )
    {
        Dispatcher.UIThread.Post(() =>
        {
            OutputProgress.Value = args.Value;
            OutputProgress.Maximum = args.Maximum;
            OutputProgress.IsIndeterminate = false;

            OutputProgress.Text =
                $"({args.Value} / {args.Maximum})"
                + (args.RunningNode != null ? $" {args.RunningNode}" : "");
        });
    }

    private void AttachRunningNodeChangedHandler(ComfyTask comfyTask)
    {
        // Do initial update
        if (comfyTask.RunningNodesHistory.TryPeek(out var lastNode))
        {
            OnRunningNodeChanged(comfyTask, lastNode);
        }

        comfyTask.RunningNodeChanged += OnRunningNodeChanged;
    }

    /// <summary>
    /// Handles the node executing updates received event from the websocket.
    /// </summary>
    protected virtual void OnRunningNodeChanged(object? sender, string? nodeName)
    {
        // Ignore if regular progress updates started
        if (sender is not ComfyTask { HasProgressUpdateStarted: false })
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            OutputProgress.IsIndeterminate = true;
            OutputProgress.Value = 100;
            OutputProgress.Maximum = 100;
            OutputProgress.Text = nodeName;
        });
    }

    public class ImageGenerationEventArgs : EventArgs
    {
        public required ComfyClient Client { get; init; }
        public required NodeDictionary Nodes { get; init; }
        public required IReadOnlyList<string> OutputNodeNames { get; init; }
        public GenerationParameters? Parameters { get; set; }
        public InferenceProjectDocument? Project { get; set; }
    }

    public class BuildPromptEventArgs : EventArgs
    {
        public ComfyNodeBuilder Builder { get; } = new();
        public GenerateOverrides Overrides { get; set; } = new();
    }
}
