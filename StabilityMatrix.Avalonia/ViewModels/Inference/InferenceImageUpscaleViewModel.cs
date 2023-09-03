using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using Refit;
using SkiaSharp;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;
using StabilityMatrix.Core.Services;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceImageUpscaleView), persistent: true)]
public partial class InferenceImageUpscaleViewModel : InferenceTabViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IModelIndexService modelIndexService;

    public IInferenceClientManager ClientManager { get; }

    public ImageGalleryCardViewModel ImageGalleryCardViewModel { get; }
    public StackCardViewModel StackCardViewModel { get; }

    public UpscalerCardViewModel UpscalerCardViewModel =>
        StackCardViewModel.GetCard<StackExpanderViewModel>().GetCard<UpscalerCardViewModel>();

    [JsonIgnore]
    public ProgressViewModel OutputProgress { get; } = new();

    [ObservableProperty]
    [property: JsonIgnore]
    private string? outputImageSource;

    public InferenceImageUpscaleViewModel(
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ServiceManager<ViewModelBase> vmFactory,
        IModelIndexService modelIndexService
    )
    {
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;
        this.modelIndexService = modelIndexService;
        ClientManager = inferenceClientManager;

        // Get sub view models from service manager

        var seedCard = vmFactory.Get<SeedCardViewModel>();
        seedCard.GenerateNewSeed();

        ImageGalleryCardViewModel = vmFactory.Get<ImageGalleryCardViewModel>();

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();

        StackCardViewModel.AddCards(
            new LoadableViewModelBase[]
            {
                // Upscaler
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Upscale";
                    stackExpander.AddCards(
                        new LoadableViewModelBase[]
                        {
                            // Post processing upscaler
                            vmFactory.Get<UpscalerCardViewModel>(),
                        }
                    );
                })
            }
        );

        // GenerateImageCommand.WithNotificationErrorHandler(notificationService);
    }

    private (NodeDictionary prompt, string[] outputs) BuildPrompt()
    {
        using var _ = new CodeTimer();

        var builder = new ComfyNodeBuilder();

        return (builder.ToNodeDictionary(), new[] { "?" });
    }

    private void OnProgressUpdateReceived(object? sender, ComfyProgressUpdateEventArgs args)
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

    private void OnPreviewImageReceived(object? sender, ComfyWebSocketImageData args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(args.ImageBytes);

            var bitmap = new Bitmap(stream);

            var currentImage = ImageGalleryCardViewModel.PreviewImage;

            ImageGalleryCardViewModel.PreviewImage = bitmap;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = true;

            currentImage?.Dispose();
        });
    }

    private async Task GenerateImageImpl(CancellationToken cancellationToken = default)
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show("Client not connected", "Please connect first");
            return;
        }

        var client = ClientManager.Client;

        var (nodes, outputNodeNames) = BuildPrompt();

        // Connect preview image handler
        client.PreviewImageReceived += OnPreviewImageReceived;

        ComfyTask? promptTask = null;
        try
        {
            // Register to interrupt if user cancels
            cancellationToken.Register(() =>
            {
                Logger.Info("Cancelling prompt");
                client
                    .InterruptPromptAsync(new CancellationTokenSource(5000).Token)
                    .SafeFireAndForget();
            });

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

            // Wait for prompt to finish
            await promptTask.Task.WaitAsync(cancellationToken);
            Logger.Trace($"Prompt task {promptTask.Id} finished");

            // Get output images
            var imageOutputs = await client.GetImagesForExecutedPromptAsync(
                promptTask.Id,
                cancellationToken
            );

            ImageGalleryCardViewModel.ImageSources.Clear();

            var images = imageOutputs[outputNodeNames[0]];
            if (images is null)
                return;

            List<ImageSource> outputImages;
            // Use local file path if available, otherwise use remote URL
            if (client.OutputImagesDir is { } outputPath)
            {
                outputImages = images
                    .Select(i => new ImageSource(i.ToFilePath(outputPath)))
                    .ToList();
            }
            else
            {
                outputImages = images
                    .Select(i => new ImageSource(i.ToUri(client.BaseAddress)))
                    .ToList();
            }

            // Download all images to make grid, if multiple
            if (outputImages.Count > 1)
            {
                var loadedImages = outputImages
                    .Select(i => SKImage.FromEncodedData(i.LocalFile?.Info.OpenRead()))
                    .ToImmutableArray();

                var grid = ImageProcessor.CreateImageGrid(loadedImages);

                // Save to disk
                var lastName = outputImages.Last().LocalFile?.Info.Name;
                var gridPath = client.OutputImagesDir!.JoinFile($"grid-{lastName}");

                await using (var fileStream = gridPath.Info.OpenWrite())
                {
                    await fileStream.WriteAsync(grid.Encode().ToArray(), cancellationToken);
                }

                // Insert to start of images
                var gridImage = new ImageSource(gridPath);
                // Preload
                await gridImage.GetBitmapAsync();
                ImageGalleryCardViewModel.ImageSources.Add(gridImage);
            }

            // Add rest of images
            foreach (var img in outputImages)
            {
                // Preload
                await img.GetBitmapAsync();
                ImageGalleryCardViewModel.ImageSources.Add(img);
            }
        }
        finally
        {
            // Disconnect progress handler
            OutputProgress.Value = 0;
            OutputProgress.Text = "";
            ImageGalleryCardViewModel.PreviewImage?.Dispose();
            ImageGalleryCardViewModel.PreviewImage = null;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = false;

            promptTask?.Dispose();
            client.PreviewImageReceived -= OnPreviewImageReceived;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateImage(CancellationToken cancellationToken = default)
    {
        try
        {
            await GenerateImageImpl(cancellationToken);
        }
        catch (OperationCanceledException e)
        {
            Logger.Debug($"[Image Upscale Canceled] {e.Message}");
        }
    }
}
