using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using ExifLibrary;
using FluentAvalonia.UI.Controls;
using NLog;
using Refit;
using Semver;
using SkiaSharp;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using Notification = DesktopNotifications.Notification;

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

    private readonly ISettingsManager settingsManager;
    private readonly RunningPackageService runningPackageService;
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
        INotificationService notificationService,
        ISettingsManager settingsManager,
        RunningPackageService runningPackageService
    )
        : base(notificationService)
    {
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.runningPackageService = runningPackageService;
        this.vmFactory = vmFactory;

        ClientManager = inferenceClientManager;

        ImageGalleryCardViewModel = vmFactory.Get<ImageGalleryCardViewModel>();
        ImageFolderCardViewModel = AddDisposable(vmFactory.Get<ImageFolderCardViewModel>());

        GenerateImageCommand.WithConditionalNotificationErrorHandler(notificationService);
    }

    /// <summary>
    /// Write an image to the default output folder
    /// </summary>
    protected Task<FilePath> WriteOutputImageAsync(
        Stream imageStream,
        ImageGenerationEventArgs args,
        int batchNum = 0,
        int batchTotal = 0,
        bool isGrid = false,
        string fileExtension = "png"
    )
    {
        var defaultOutputDir = settingsManager.ImagesInferenceDirectory;
        defaultOutputDir.Create();

        return WriteOutputImageAsync(
            imageStream,
            defaultOutputDir,
            args,
            batchNum,
            batchTotal,
            isGrid,
            fileExtension
        );
    }

    /// <summary>
    /// Write an image to an output folder
    /// </summary>
    protected async Task<FilePath> WriteOutputImageAsync(
        Stream imageStream,
        DirectoryPath outputDir,
        ImageGenerationEventArgs args,
        int batchNum = 0,
        int batchTotal = 0,
        bool isGrid = false,
        string fileExtension = "png"
    )
    {
        var formatTemplateStr = settingsManager.Settings.InferenceOutputImageFileNameFormat;

        var formatProvider = new FileNameFormatProvider
        {
            GenerationParameters = args.Parameters,
            ProjectType = args.Project?.ProjectType,
            ProjectName = ProjectFile?.NameWithoutExtension
        };

        // Parse to format
        if (
            string.IsNullOrEmpty(formatTemplateStr)
            || !FileNameFormat.TryParse(formatTemplateStr, formatProvider, out var format)
        )
        {
            // Fallback to default
            Logger.Warn(
                "Failed to parse format template: {FormatTemplate}, using default",
                formatTemplateStr
            );

            format = FileNameFormat.Parse(FileNameFormat.DefaultTemplate, formatProvider);
        }

        if (isGrid)
        {
            format = format.WithGridPrefix();
        }

        if (batchNum >= 1 && batchTotal > 1)
        {
            format = format.WithBatchPostFix(batchNum, batchTotal);
        }

        var fileName = format.GetFileName();
        var file = outputDir.JoinFile($"{fileName}.{fileExtension}");

        // Until the file is free, keep adding _{i} to the end
        for (var i = 0; i < 100; i++)
        {
            if (!file.Exists)
                break;

            file = outputDir.JoinFile($"{fileName}_{i + 1}.{fileExtension}");
        }

        // If that fails, append an 7-char uuid
        if (file.Exists)
        {
            var uuid = Guid.NewGuid().ToString("N")[..7];
            file = outputDir.JoinFile($"{fileName}_{uuid}.{fileExtension}");
        }

        if (file.Info.DirectoryName != null)
        {
            Directory.CreateDirectory(file.Info.DirectoryName);
        }

        await using var fileStream = file.Info.OpenWrite();
        await imageStream.CopyToAsync(fileStream);

        return file;
    }

    /// <summary>
    /// Builds the image generation prompt
    /// </summary>
    protected virtual void BuildPrompt(BuildPromptEventArgs args) { }

    /// <summary>
    /// Uploads files required for the prompt
    /// </summary>
    protected virtual async Task UploadPromptFiles(
        IEnumerable<(string SourcePath, string DestinationRelativePath)> files,
        ComfyClient client
    )
    {
        foreach (var (sourcePath, destinationRelativePath) in files)
        {
            Logger.Debug(
                "Uploading prompt file {SourcePath} to relative path {DestinationPath}",
                sourcePath,
                destinationRelativePath
            );

            await client.UploadFileAsync(sourcePath, destinationRelativePath);
        }
    }

    /// <summary>
    /// Gets ImageSources that need to be uploaded as inputs
    /// </summary>
    protected virtual IEnumerable<ImageSource> GetInputImages()
    {
        return Enumerable.Empty<ImageSource>();
    }

    protected async Task UploadInputImages(ComfyClient client)
    {
        foreach (var image in GetInputImages())
        {
            await ClientManager.UploadInputImageAsync(image);
        }
    }

    public async Task RunCustomGeneration(
        InferenceQueueCustomPromptEventArgs args,
        CancellationToken cancellationToken = default
    )
    {
        if (ClientManager.Client is not { } client)
        {
            throw new InvalidOperationException("Client is not connected");
        }

        var generationArgs = new ImageGenerationEventArgs
        {
            Client = client,
            Nodes = args.Builder.ToNodeDictionary(),
            OutputNodeNames = args.Builder.Connections.OutputNodeNames.ToArray(),
            Project = InferenceProjectDocument.FromLoadable(this),
            FilesToTransfer = args.FilesToTransfer,
            Parameters = new GenerationParameters(),
            ClearOutputImages = true
        };

        await RunGeneration(generationArgs, cancellationToken);
    }

    /// <summary>
    /// Runs a generation task
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if args.Parameters or args.Project are null</exception>
    protected async Task RunGeneration(ImageGenerationEventArgs args, CancellationToken cancellationToken)
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

        // Only check extensions for first batch index
        if (args.BatchIndex == 0)
        {
            if (!await CheckPromptExtensionsInstalled(args.Nodes))
            {
                throw new ValidationException("Prompt extensions not installed");
            }
        }

        // Upload input images
        await UploadInputImages(client);

        // Upload required files
        await UploadPromptFiles(args.FilesToTransfer, client);

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
                        try
                        {
                            var delayTime = 250 - (int)timer.ElapsedMilliseconds;
                            if (delayTime > 0)
                            {
                                await Task.Delay(delayTime, cancellationToken);
                            }

                            // ReSharper disable once AccessToDisposedClosure
                            AttachRunningNodeChangedHandler(promptTask);
                        }
                        catch (TaskCanceledException) { }
                    },
                    cancellationToken
                )
                .SafeFireAndForget(ex =>
                {
                    if (ex is TaskCanceledException)
                        return;

                    Logger.Error(ex, "Error while attaching running node change handler");
                });

            // Wait for prompt to finish
            try
            {
                await promptTask.Task.WaitAsync(cancellationToken);
                Logger.Debug($"Prompt task {promptTask.Id} finished");
            }
            catch (ComfyNodeException e)
            {
                Logger.Warn(e, "Comfy node exception while queuing prompt");
                await DialogHelper
                    .CreateJsonDialog(e.JsonData, "Comfy Error", "Node execution encountered an error")
                    .ShowAsync();
                return;
            }

            // Get output images
            var imageOutputs = await client.GetImagesForExecutedPromptAsync(promptTask.Id, cancellationToken);

            if (imageOutputs.Values.All(images => images is null or { Count: 0 }))
            {
                // No images match
                notificationService.Show(
                    "No output",
                    "Did not receive any output images",
                    NotificationType.Warning
                );
                return;
            }

            // Disable cancellation
            await promptInterrupt.DisposeAsync();

            if (args.ClearOutputImages)
            {
                ImageGalleryCardViewModel.ImageSources.Clear();
            }

            var outputImages = await ProcessAllOutputImages(imageOutputs, args);

            var notificationImage = outputImages.FirstOrDefault()?.LocalFile;

            await notificationService.ShowAsync(
                NotificationKey.Inference_PromptCompleted,
                new Notification
                {
                    Title = "Prompt Completed",
                    Body = $"Prompt [{promptTask.Id[..7].ToLower()}] completed successfully",
                    BodyImagePath = notificationImage?.FullPath
                }
            );
        }
        finally
        {
            // Disconnect progress handler
            client.PreviewImageReceived -= OnPreviewImageReceived;

            // Clear progress
            OutputProgress.ClearProgress();
            // ImageGalleryCardViewModel.PreviewImage?.Dispose();
            ImageGalleryCardViewModel.PreviewImage = null;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = false;

            // Cleanup tasks
            promptTask?.Dispose();
        }
    }

    private async Task<IEnumerable<ImageSource>> ProcessAllOutputImages(
        IReadOnlyDictionary<string, List<ComfyImage>?> images,
        ImageGenerationEventArgs args
    )
    {
        var results = new List<ImageSource>();

        foreach (var (nodeName, imageList) in images)
        {
            if (imageList is null)
            {
                Logger.Warn("No images for node {NodeName}", nodeName);
                continue;
            }

            results.AddRange(await ProcessOutputImages(imageList, args, nodeName.Replace('_', ' ')));
        }

        return results;
    }

    /// <summary>
    /// Handles image output metadata for generation runs
    /// </summary>
    private async Task<List<ImageSource>> ProcessOutputImages(
        IReadOnlyCollection<ComfyImage> images,
        ImageGenerationEventArgs args,
        string? imageLabel = null
    )
    {
        var client = args.Client;

        // Write metadata to images
        var outputImagesBytes = new List<byte[]>();
        var outputImages = new List<ImageSource>();

        foreach (var (i, comfyImage) in images.Enumerate())
        {
            Logger.Debug("Downloading image: {FileName}", comfyImage.FileName);
            var imageStream = await client.GetImageStreamAsync(comfyImage);

            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);

            var imageArray = ms.ToArray();
            outputImagesBytes.Add(imageArray);

            var parameters = args.Parameters!;
            var project = args.Project!;

            // Lock seed
            project.TryUpdateModel<SeedCardModel>("Seed", model => model with { IsRandomizeEnabled = false });

            // Seed and batch override for batches
            if (images.Count > 1 && project.ProjectType is InferenceProjectType.TextToImage)
            {
                project = (InferenceProjectDocument)project.Clone();

                // Set batch size indexes
                project.TryUpdateModel(
                    "BatchSize",
                    node =>
                    {
                        node[nameof(BatchSizeCardViewModel.BatchCount)] = 1;
                        node[nameof(BatchSizeCardViewModel.IsBatchIndexEnabled)] = true;
                        node[nameof(BatchSizeCardViewModel.BatchIndex)] = i + 1;
                        return node;
                    }
                );
            }

            if (comfyImage.FileName.EndsWith(".png"))
            {
                var bytesWithMetadata = PngDataHelper.AddMetadata(imageArray, parameters, project);

                // Write using generated name
                var filePath = await WriteOutputImageAsync(
                    new MemoryStream(bytesWithMetadata),
                    args,
                    i + 1,
                    images.Count
                );

                outputImages.Add(new ImageSource(filePath) { Label = imageLabel });
                EventManager.Instance.OnImageFileAdded(filePath);
            }
            else if (comfyImage.FileName.EndsWith(".webp"))
            {
                var opts = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter() }
                };
                var paramsJson = JsonSerializer.Serialize(parameters, opts);
                var smProject = JsonSerializer.Serialize(project, opts);
                var metadata = new Dictionary<ExifTag, string>
                {
                    { ExifTag.ImageDescription, paramsJson },
                    { ExifTag.Software, smProject }
                };

                var bytesWithMetadata = ImageMetadata.AddMetadataToWebp(imageArray, metadata);

                // Write using generated name
                var filePath = await WriteOutputImageAsync(
                    new MemoryStream(bytesWithMetadata.ToArray()),
                    args,
                    i + 1,
                    images.Count,
                    fileExtension: Path.GetExtension(comfyImage.FileName).Replace(".", "")
                );

                outputImages.Add(new ImageSource(filePath) { Label = imageLabel });
                EventManager.Instance.OnImageFileAdded(filePath);
            }
            else
            {
                // Write using generated name
                var filePath = await WriteOutputImageAsync(
                    new MemoryStream(imageArray),
                    args,
                    i + 1,
                    images.Count,
                    fileExtension: Path.GetExtension(comfyImage.FileName).Replace(".", "")
                );

                outputImages.Add(new ImageSource(filePath) { Label = imageLabel });
                EventManager.Instance.OnImageFileAdded(filePath);
            }
        }

        // Download all images to make grid, if multiple
        if (outputImages.Count > 1)
        {
            var loadedImages = outputImagesBytes.Select(SKImage.FromEncodedData).ToImmutableArray();

            var project = args.Project!;

            // Lock seed
            project.TryUpdateModel<SeedCardModel>("Seed", model => model with { IsRandomizeEnabled = false });

            var grid = ImageProcessor.CreateImageGrid(loadedImages);
            var gridBytes = grid.Encode().ToArray();
            var gridBytesWithMetadata = PngDataHelper.AddMetadata(gridBytes, args.Parameters!, args.Project!);

            // Save to disk
            var gridPath = await WriteOutputImageAsync(
                new MemoryStream(gridBytesWithMetadata),
                args,
                isGrid: true
            );

            // Insert to start of images
            var gridImage = new ImageSource(gridPath);
            outputImages.Insert(0, gridImage);
            EventManager.Instance.OnImageFileAdded(gridPath);
        }

        foreach (var img in outputImages)
        {
            // Preload
            await img.GetBitmapAsync();
            // Add images
            ImageGalleryCardViewModel.ImageSources.Add(img);
        }

        return outputImages;
    }

    /// <summary>
    /// Implementation for Generate Image
    /// </summary>
    protected virtual Task GenerateImageImpl(GenerateOverrides overrides, CancellationToken cancellationToken)
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
            Logger.Debug("Image Generation Canceled");
        }
        catch (ValidationException e)
        {
            Logger.Debug("Image Generation Validation Error: {Message}", e.Message);
            notificationService.Show("Validation Error", e.Message, NotificationType.Error);
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
    /// Shows a dialog and return false if prompt required extensions not installed
    /// </summary>
    private async Task<bool> CheckPromptExtensionsInstalled(NodeDictionary nodeDictionary)
    {
        // Get prompt required extensions
        // Just static for now but could do manifest lookup when we support custom workflows
        var requiredExtensionSpecifiers = nodeDictionary
            .RequiredExtensions.DistinctBy(ext => ext.Name)
            .ToList();

        // Skip if no extensions required
        if (requiredExtensionSpecifiers.Count == 0)
        {
            return true;
        }

        // Get installed extensions
        var localPackagePair = ClientManager.Client?.LocalServerPackage.Unwrap()!;
        var manager = localPackagePair.BasePackage.ExtensionManager.Unwrap();

        var localExtensions = (
            await ((GitPackageExtensionManager)manager).GetInstalledExtensionsLiteAsync(
                localPackagePair.InstalledPackage
            )
        ).ToList();

        var localExtensionsByGitUrl = localExtensions
            .Where(ext => ext.GitRepositoryUrl is not null)
            .ToDictionary(ext => ext.GitRepositoryUrl!, ext => ext);

        var requiredExtensionReferences = requiredExtensionSpecifiers
            .Select(specifier => specifier.Name)
            .ToHashSet();

        var missingExtensions = new List<ExtensionSpecifier>();
        var outOfDateExtensions =
            new List<(ExtensionSpecifier Specifier, InstalledPackageExtension Installed)>();

        // Check missing extensions and out of date extensions
        foreach (var specifier in requiredExtensionSpecifiers)
        {
            if (!localExtensionsByGitUrl.TryGetValue(specifier.Name, out var localExtension))
            {
                missingExtensions.Add(specifier);
                continue;
            }

            // Check if constraint is specified
            if (specifier.Constraint is not null && specifier.TryGetSemVersionRange(out var semVersionRange))
            {
                // Get version to compare
                localExtension = await manager.GetInstalledExtensionInfoAsync(localExtension);

                // Try to parse local tag to semver
                if (
                    localExtension.Version?.Tag is not null
                    && SemVersion.TryParse(
                        localExtension.Version.Tag,
                        SemVersionStyles.AllowV,
                        out var localSemVersion
                    )
                )
                {
                    // Check if not satisfied
                    if (!semVersionRange.Contains(localSemVersion))
                    {
                        outOfDateExtensions.Add((specifier, localExtension));
                    }
                }
            }
        }

        if (missingExtensions.Count == 0 && outOfDateExtensions.Count == 0)
        {
            return true;
        }

        var dialog = DialogHelper.CreateMarkdownDialog(
            $"#### The following extensions are required for this workflow:\n"
                + $"{string.Join("\n- ", missingExtensions.Select(ext => ext.Name))}"
                + $"{string.Join("\n- ", outOfDateExtensions.Select(pair => $"{pair.Item1.Name} {pair.Specifier.Constraint} {pair.Specifier.Version} (Current Version: {pair.Installed.Version?.Tag})"))}",
            "Install Required Extensions?"
        );

        dialog.IsPrimaryButtonEnabled = true;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.PrimaryButtonText =
            $"{Resources.Action_Install} ({localPackagePair.InstalledPackage.DisplayName.ToRepr()} will restart)";
        dialog.CloseButtonText = Resources.Action_Cancel;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var manifestExtensionsMap = await manager.GetManifestExtensionsMapAsync(
                manager.GetManifests(localPackagePair.InstalledPackage)
            );

            var steps = new List<IPackageStep>();

            // Add install for missing extensions
            foreach (var missingExtension in missingExtensions)
            {
                if (!manifestExtensionsMap.TryGetValue(missingExtension.Name, out var extension))
                {
                    Logger.Warn(
                        "Extension {MissingExtensionUrl} not found in manifests",
                        missingExtension.Name
                    );
                    continue;
                }

                steps.Add(new InstallExtensionStep(manager, localPackagePair.InstalledPackage, extension));
            }

            // Add update for out of date extensions
            foreach (var (specifier, installed) in outOfDateExtensions)
            {
                if (!manifestExtensionsMap.TryGetValue(specifier.Name, out var extension))
                {
                    Logger.Warn("Extension {MissingExtensionUrl} not found in manifests", specifier.Name);
                    continue;
                }

                steps.Add(new UpdateExtensionStep(manager, localPackagePair.InstalledPackage, installed));
            }

            var runner = new PackageModificationRunner
            {
                ShowDialogOnStart = true,
                ModificationCompleteTitle = "Extensions Installed",
                ModificationCompleteMessage = "Finished installing required extensions"
            };
            EventManager.Instance.OnPackageInstallProgressAdded(runner);

            runner
                .ExecuteSteps(steps)
                .ContinueWith(async _ =>
                {
                    if (runner.Failed)
                        return;

                    // Restart Package
                    try
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await runningPackageService.StopPackage(localPackagePair.InstalledPackage.Id);
                            await runningPackageService.StartPackage(localPackagePair.InstalledPackage);
                        });
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Error while restarting package");

                        notificationService.ShowPersistent(
                            new AppException(
                                "Could not restart package",
                                "Please manually restart the package for extension changes to take effect"
                            )
                        );
                    }
                })
                .SafeFireAndForget();
        }

        return false;
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
    protected virtual void OnProgressUpdateReceived(object? sender, ComfyProgressUpdateEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            OutputProgress.Value = args.Value;
            OutputProgress.Maximum = args.Maximum;
            OutputProgress.IsIndeterminate = false;

            OutputProgress.Text =
                $"({args.Value} / {args.Maximum})" + (args.RunningNode != null ? $" {args.RunningNode}" : "");
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
        public int BatchIndex { get; init; }
        public GenerationParameters? Parameters { get; init; }
        public InferenceProjectDocument? Project { get; init; }
        public bool ClearOutputImages { get; init; } = true;
        public List<(string SourcePath, string DestinationRelativePath)> FilesToTransfer { get; init; } = [];
    }

    public class BuildPromptEventArgs : EventArgs
    {
        public ComfyNodeBuilder Builder { get; } = new();
        public GenerateOverrides Overrides { get; init; } = new();
        public long? SeedOverride { get; init; }
        public List<(string SourcePath, string DestinationRelativePath)> FilesToTransfer { get; init; } = [];

        public ModuleApplyStepEventArgs ToModuleApplyStepEventArgs()
        {
            var overrides = new Dictionary<Type, bool>();

            if (Overrides.IsHiresFixEnabled.HasValue)
            {
                overrides[typeof(HiresFixModule)] = Overrides.IsHiresFixEnabled.Value;
            }

            return new ModuleApplyStepEventArgs
            {
                Builder = Builder,
                IsEnabledOverrides = overrides,
                FilesToTransfer = FilesToTransfer
            };
        }

        public static implicit operator ModuleApplyStepEventArgs(BuildPromptEventArgs args)
        {
            return args.ToModuleApplyStepEventArgs();
        }
    }
}
