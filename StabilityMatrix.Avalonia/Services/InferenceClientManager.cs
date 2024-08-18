using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manager for the current inference client
/// Has observable shared properties for shared info like model names
/// </summary>
[Singleton(typeof(IInferenceClientManager))]
public partial class InferenceClientManager : ObservableObject, IInferenceClientManager
{
    private readonly ILogger<InferenceClientManager> logger;
    private readonly IApiFactory apiFactory;
    private readonly IModelIndexService modelIndexService;
    private readonly ISettingsManager settingsManager;
    private readonly ICompletionProvider completionProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(CanUserConnect))]
    private ComfyClient? client;

    [MemberNotNullWhen(true, nameof(Client))]
    public bool IsConnected => Client is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUserConnect))]
    private bool isConnecting;

    /// <inheritdoc />
    public bool CanUserConnect => !IsConnected && !IsConnecting;

    /// <inheritdoc />
    public bool CanUserDisconnect => IsConnected && !IsConnecting;

    private readonly SourceCache<HybridModelFile, string> modelsSource = new(p => p.GetId());

    public IObservableCollection<HybridModelFile> Models { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> vaeModelsSource = new(p => p.GetId());

    private readonly SourceCache<HybridModelFile, string> vaeModelsDefaults = new(p => p.GetId());

    public IObservableCollection<HybridModelFile> VaeModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> controlNetModelsSource = new(p => p.GetId());

    private readonly SourceCache<HybridModelFile, string> downloadableControlNetModelsSource =
        new(p => p.GetId());

    public IObservableCollection<HybridModelFile> ControlNetModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> loraModelsSource = new(p => p.GetId());

    public IObservableCollection<HybridModelFile> LoraModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> promptExpansionModelsSource = new(p => p.GetId());

    private readonly SourceCache<HybridModelFile, string> downloadablePromptExpansionModelsSource =
        new(p => p.GetId());

    public IObservableCollection<HybridModelFile> PromptExpansionModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<ComfySampler, string> samplersSource = new(p => p.Name);

    public IObservableCollection<ComfySampler> Samplers { get; } =
        new ObservableCollectionExtended<ComfySampler>();

    private readonly SourceCache<ComfyUpscaler, string> modelUpscalersSource = new(p => p.Name);

    private readonly SourceCache<ComfyUpscaler, string> latentUpscalersSource = new(p => p.Name);

    private readonly SourceCache<ComfyUpscaler, string> downloadableUpscalersSource = new(p => p.Name);

    public IObservableCollection<ComfyUpscaler> Upscalers { get; } =
        new ObservableCollectionExtended<ComfyUpscaler>();

    private readonly SourceCache<ComfyScheduler, string> schedulersSource = new(p => p.Name);

    public IObservableCollection<ComfyScheduler> Schedulers { get; } =
        new ObservableCollectionExtended<ComfyScheduler>();

    public IObservableCollection<ComfyAuxPreprocessor> Preprocessors { get; } =
        new ObservableCollectionExtended<ComfyAuxPreprocessor>();

    private readonly SourceCache<ComfyAuxPreprocessor, string> preprocessorsSource = new(p => p.Value);

    public IObservableCollection<HybridModelFile> UltralyticsModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> ultralyticsModelsSource = new(p => p.GetId());

    private readonly SourceCache<HybridModelFile, string> downloadableUltralyticsModelsSource =
        new(p => p.GetId());

    public IObservableCollection<HybridModelFile> SamModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> samModelsSource = new(p => p.GetId());

    private readonly SourceCache<HybridModelFile, string> downloadableSamModelsSource = new(p => p.GetId());

    private readonly SourceCache<HybridModelFile, string> unetModelsSource = new(p => p.GetId());

    public IObservableCollection<HybridModelFile> UnetModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    private readonly SourceCache<HybridModelFile, string> clipModelsSource = new(p => p.GetId());
    private readonly SourceCache<HybridModelFile, string> downloadableClipModelsSource = new(p => p.GetId());

    public IObservableCollection<HybridModelFile> ClipModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public InferenceClientManager(
        ILogger<InferenceClientManager> logger,
        IApiFactory apiFactory,
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager,
        ICompletionProvider completionProvider
    )
    {
        this.logger = logger;
        this.apiFactory = apiFactory;
        this.modelIndexService = modelIndexService;
        this.settingsManager = settingsManager;
        this.completionProvider = completionProvider;

        modelsSource
            .Connect()
            .SortBy(
                f => f.ShortDisplayName,
                SortDirection.Ascending,
                SortOptimisations.ComparesImmutableValuesOnly
            )
            .DeferUntilLoaded()
            .Bind(Models)
            .Subscribe();

        controlNetModelsSource
            .Connect()
            .Or(downloadableControlNetModelsSource.Connect())
            .Sort(
                SortExpressionComparer<HybridModelFile>
                    .Ascending(f => f.Type)
                    .ThenByAscending(f => f.ShortDisplayName)
            )
            .DeferUntilLoaded()
            .Bind(ControlNetModels)
            .Subscribe();

        loraModelsSource
            .Connect()
            .DeferUntilLoaded()
            .SortAndBind(
                LoraModels,
                SortExpressionComparer<HybridModelFile>
                    .Ascending(f => f.ShortDisplayName)
                    .ThenByAscending(f => f.Type)
            )
            .Subscribe();

        promptExpansionModelsSource
            .Connect()
            .Or(downloadablePromptExpansionModelsSource.Connect())
            .Sort(
                SortExpressionComparer<HybridModelFile>
                    .Ascending(f => f.Type)
                    .ThenByAscending(f => f.ShortDisplayName)
            )
            .DeferUntilLoaded()
            .Bind(PromptExpansionModels)
            .Subscribe();

        ultralyticsModelsSource
            .Connect()
            .Or(downloadableUltralyticsModelsSource.Connect())
            .Sort(
                SortExpressionComparer<HybridModelFile>
                    .Ascending(f => f.Type)
                    .ThenByAscending(f => f.ShortDisplayName)
            )
            .DeferUntilLoaded()
            .Bind(UltralyticsModels)
            .Subscribe();

        samModelsSource
            .Connect()
            .Or(downloadableSamModelsSource.Connect())
            .Sort(
                SortExpressionComparer<HybridModelFile>
                    .Ascending(f => f.Type)
                    .ThenByAscending(f => f.ShortDisplayName)
            )
            .DeferUntilLoaded()
            .Bind(SamModels)
            .Subscribe();

        unetModelsSource
            .Connect()
            .SortBy(
                f => f.ShortDisplayName,
                SortDirection.Ascending,
                SortOptimisations.ComparesImmutableValuesOnly
            )
            .DeferUntilLoaded()
            .Bind(UnetModels)
            .Subscribe();

        clipModelsSource
            .Connect()
            .Or(downloadableClipModelsSource.Connect())
            .SortBy(
                f => f.ShortDisplayName,
                SortDirection.Ascending,
                SortOptimisations.ComparesImmutableValuesOnly
            )
            .DeferUntilLoaded()
            .Bind(ClipModels)
            .Subscribe();

        vaeModelsDefaults.AddOrUpdate(HybridModelFile.Default);

        vaeModelsDefaults.Connect().Or(vaeModelsSource.Connect()).Bind(VaeModels).Subscribe();

        samplersSource.Connect().DeferUntilLoaded().Bind(Samplers).Subscribe();

        latentUpscalersSource
            .Connect()
            .Or(modelUpscalersSource.Connect())
            .Or(downloadableUpscalersSource.Connect())
            .Sort(SortExpressionComparer<ComfyUpscaler>.Ascending(f => f.Type).ThenByAscending(f => f.Name))
            .Bind(Upscalers)
            .Subscribe();

        schedulersSource.Connect().DeferUntilLoaded().Bind(Schedulers).Subscribe();

        preprocessorsSource.Connect().DeferUntilLoaded().Bind(Preprocessors).Subscribe();

        settingsManager.RegisterOnLibraryDirSet(_ =>
        {
            Dispatcher.UIThread.Post(ResetSharedProperties, DispatcherPriority.Background);
        });

        EventManager.Instance.ModelIndexChanged += (_, _) =>
        {
            logger.LogDebug("Model index changed, reloading shared properties for Inference");

            if (!settingsManager.IsLibraryDirSet)
                return;

            ResetSharedProperties();

            if (IsConnected)
            {
                LoadSharedPropertiesAsync()
                    .SafeFireAndForget(
                        onException: ex => logger.LogError(ex, "Error loading shared properties")
                    );
            }
        };
    }

    [MemberNotNull(nameof(Client))]
    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Client is not connected");
    }

    private async Task LoadSharedPropertiesAsync()
    {
        EnsureConnected();

        // Get model names
        if (await Client.GetModelNamesAsync() is { } modelNames)
        {
            modelsSource.EditDiff(modelNames.Select(HybridModelFile.FromRemote), HybridModelFile.Comparer);
        }

        // Get control net model names
        if (
            await Client.GetNodeOptionNamesAsync("ControlNetLoader", "control_net_name") is
            { } controlNetModelNames
        )
        {
            controlNetModelsSource.EditDiff(
                controlNetModelNames.Select(HybridModelFile.FromRemote),
                HybridModelFile.Comparer
            );
        }

        // Get Lora model names
        if (await Client.GetNodeOptionNamesAsync("LoraLoader", "lora_name") is { } loraModelNames)
        {
            loraModelsSource.EditDiff(
                loraModelNames.Select(HybridModelFile.FromRemote),
                HybridModelFile.Comparer
            );
        }

        // Get Ultralytics model names
        if (
            await Client.GetOptionalNodeOptionNamesAsync("UltralyticsDetectorProvider", "model_name") is
            { } ultralyticsModelNames
        )
        {
            IEnumerable<HybridModelFile> models =
            [
                HybridModelFile.None,
                ..ultralyticsModelNames.Select(HybridModelFile.FromRemote)
            ];
            ultralyticsModelsSource.EditDiff(models, HybridModelFile.Comparer);
        }

        // Get SAM model names
        if (await Client.GetOptionalNodeOptionNamesAsync("SAMLoader", "model_name") is { } samModelNames)
        {
            IEnumerable<HybridModelFile> models =
            [
                HybridModelFile.None,
                ..samModelNames.Select(HybridModelFile.FromRemote)
            ];
            samModelsSource.EditDiff(models, HybridModelFile.Comparer);
        }

        // Prompt Expansion indexing is local only

        // Fetch sampler names from KSampler node
        if (await Client.GetSamplerNamesAsync() is { } samplerNames)
        {
            samplersSource.EditDiff(
                samplerNames.Select(name => new ComfySampler(name)),
                ComfySampler.Comparer
            );
        }

        // Upscalers is latent and esrgan combined

        // Add latent upscale methods from LatentUpscale node
        if (
            await Client.GetNodeOptionNamesAsync("LatentUpscale", "upscale_method") is { } latentUpscalerNames
        )
        {
            latentUpscalersSource.EditDiff(
                latentUpscalerNames.Select(s => new ComfyUpscaler(s, ComfyUpscalerType.Latent)),
                ComfyUpscaler.Comparer
            );

            logger.LogTrace("Loaded latent upscale methods: {@Upscalers}", latentUpscalerNames);
        }

        // Add Model upscale methods
        if (
            await Client.GetNodeOptionNamesAsync("UpscaleModelLoader", "model_name") is { } modelUpscalerNames
        )
        {
            modelUpscalersSource.EditDiff(
                modelUpscalerNames.Select(s => new ComfyUpscaler(s, ComfyUpscalerType.ESRGAN)),
                ComfyUpscaler.Comparer
            );
            logger.LogTrace("Loaded model upscale methods: {@Upscalers}", modelUpscalerNames);
        }

        // Add scheduler names from Scheduler node
        if (await Client.GetNodeOptionNamesAsync("KSampler", "scheduler") is { } schedulerNames)
        {
            schedulersSource.Edit(updater =>
            {
                updater.AddOrUpdate(
                    schedulerNames
                        .Where(n => !schedulersSource.Keys.Contains(n))
                        .Select(s => new ComfyScheduler(s))
                );
            });
            logger.LogTrace("Loaded scheduler methods: {@Schedulers}", schedulerNames);
        }

        // Add preprocessor names from Inference_Core_AIO_Preprocessor node (might not exist if no extension)
        if (
            await Client.GetOptionalNodeOptionNamesAsync("Inference_Core_AIO_Preprocessor", "preprocessor") is
            { } preprocessorNames
        )
        {
            preprocessorsSource.EditDiff(preprocessorNames.Select(n => new ComfyAuxPreprocessor(n)));
        }

        // Get Unet model names from UNETLoader node
        if (await Client.GetNodeOptionNamesAsync("UNETLoader", "unet_name") is { } unetModelNames)
        {
            var unetModels = unetModelNames.Select(HybridModelFile.FromRemote);

            if (
                await Client.GetRequiredNodeOptionNamesFromOptionalNodeAsync("UnetLoaderGGUF", "unet_name") is
                { } ggufModelNames
            )
            {
                unetModels = unetModels.Concat(ggufModelNames.Select(HybridModelFile.FromRemote));
            }

            unetModelsSource.EditDiff(unetModels, HybridModelFile.Comparer);
        }

        // Get CLIP model names from DualCLIPLoader node
        if (await Client.GetNodeOptionNamesAsync("DualCLIPLoader", "clip_name1") is { } clipModelNames)
        {
            clipModelsSource.EditDiff(
                clipModelNames.Select(HybridModelFile.FromRemote),
                HybridModelFile.Comparer
            );
        }
    }

    /// <summary>
    /// Clears shared properties and sets them to local defaults
    /// </summary>
    private void ResetSharedProperties()
    {
        // Load local models
        modelsSource.EditDiff(
            modelIndexService
                .FindByModelType(SharedFolderType.StableDiffusion)
                .Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Load local control net models
        controlNetModelsSource.EditDiff(
            modelIndexService.FindByModelType(SharedFolderType.ControlNet).Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Downloadable ControlNet models
        var downloadableControlNets = RemoteModels.ControlNetModels.Where(
            u => !controlNetModelsSource.Lookup(u.GetId()).HasValue
        );
        downloadableControlNetModelsSource.EditDiff(downloadableControlNets, HybridModelFile.Comparer);

        // Load local Lora / LyCORIS models
        loraModelsSource.EditDiff(
            modelIndexService
                .FindByModelType(SharedFolderType.Lora | SharedFolderType.LyCORIS)
                .Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Load local prompt expansion models
        promptExpansionModelsSource.EditDiff(
            modelIndexService
                .FindByModelType(SharedFolderType.PromptExpansion)
                .Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Downloadable PromptExpansion models
        downloadablePromptExpansionModelsSource.EditDiff(
            RemoteModels.PromptExpansionModels.Where(
                u => !promptExpansionModelsSource.Lookup(u.GetId()).HasValue
            ),
            HybridModelFile.Comparer
        );

        // Load local VAE models
        vaeModelsSource.EditDiff(
            modelIndexService.FindByModelType(SharedFolderType.VAE).Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Load Ultralytics models
        IEnumerable<HybridModelFile> ultralyticsModels =
        [
            HybridModelFile.None,
            ..modelIndexService
            .FindByModelType(SharedFolderType.Ultralytics)
            .Select(HybridModelFile.FromLocal)
        ];
        ultralyticsModelsSource.EditDiff(ultralyticsModels, HybridModelFile.Comparer);

        var downloadableUltralyticsModels = RemoteModels.UltralyticsModelFiles.Where(
            u => !ultralyticsModelsSource.Lookup(u.GetId()).HasValue
        );
        downloadableUltralyticsModelsSource.EditDiff(downloadableUltralyticsModels, HybridModelFile.Comparer);

        // Load SAM models
        IEnumerable<HybridModelFile> samModels =
        [
            HybridModelFile.None,
            ..modelIndexService
                .FindByModelType(SharedFolderType.Sams)
                .Select(HybridModelFile.FromLocal)
        ];
        samModelsSource.EditDiff(samModels, HybridModelFile.Comparer);

        var downloadableSamModels = RemoteModels.SamModelFiles.Where(
            u => !samModelsSource.Lookup(u.GetId()).HasValue
        );
        downloadableSamModelsSource.EditDiff(downloadableSamModels, HybridModelFile.Comparer);

        unetModelsSource.EditDiff(
            modelIndexService.FindByModelType(SharedFolderType.Unet).Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        clipModelsSource.EditDiff(
            modelIndexService.FindByModelType(SharedFolderType.CLIP).Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        var downloadableClipModels = RemoteModels.ClipModelFiles.Where(
            u => !clipModelsSource.Lookup(u.GetId()).HasValue
        );
        downloadableClipModelsSource.EditDiff(downloadableClipModels, HybridModelFile.Comparer);

        samplersSource.EditDiff(ComfySampler.Defaults, ComfySampler.Comparer);

        latentUpscalersSource.EditDiff(ComfyUpscaler.Defaults, ComfyUpscaler.Comparer);

        schedulersSource.EditDiff(ComfyScheduler.Defaults, ComfyScheduler.Comparer);

        // Load Upscalers
        modelUpscalersSource.EditDiff(
            modelIndexService
                .FindByModelType(
                    SharedFolderType.ESRGAN | SharedFolderType.RealESRGAN | SharedFolderType.SwinIR
                )
                .Select(m => new ComfyUpscaler(m.FileName, ComfyUpscalerType.ESRGAN)),
            ComfyUpscaler.Comparer
        );

        // Remote upscalers
        var remoteUpscalers = ComfyUpscaler.DefaultDownloadableModels.Where(
            u => !modelUpscalersSource.Lookup(u.Name).HasValue
        );
        downloadableUpscalersSource.EditDiff(remoteUpscalers, ComfyUpscaler.Comparer);

        // Default Preprocessors
        preprocessorsSource.EditDiff(ComfyAuxPreprocessor.Defaults);
    }

    /// <inheritdoc />
    public async Task UploadInputImageAsync(ImageSource image, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var uploadName = await image.GetHashGuidFileNameAsync();

        if (image.LocalFile is { } localFile)
        {
            logger.LogDebug("Uploading image {FileName} as {UploadName}", localFile.Name, uploadName);

            // For pngs, strip metadata since Pillow can't handle some valid files?
            if (localFile.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = PngDataHelper.RemoveMetadata(
                    await localFile.ReadAllBytesAsync(cancellationToken)
                );
                using var stream = new MemoryStream(bytes);

                await Client.UploadImageAsync(stream, uploadName, cancellationToken);
            }
            else
            {
                await using var stream = localFile.Info.OpenRead();

                await Client.UploadImageAsync(stream, uploadName, cancellationToken);
            }
        }
        else
        {
            logger.LogDebug("Uploading bitmap as {UploadName}", uploadName);

            if (await image.GetBitmapAsync() is not { } bitmap)
            {
                throw new InvalidOperationException("Failed to get bitmap from image");
            }

            await using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;

            await Client.UploadImageAsync(ms, uploadName, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task CopyImageToInputAsync(FilePath imageFile, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return;

        if (Client.InputImagesDir is not { } inputImagesDir)
        {
            throw new InvalidOperationException("InputImagesDir is null");
        }

        var inferenceInputs = inputImagesDir.JoinDir("Inference");
        inferenceInputs.Create();

        var destination = inferenceInputs.JoinFile(imageFile.Name);

        // Read to SKImage then write to file, to prevent errors from metadata
        await Task.Run(
            () =>
            {
                using var imageStream = imageFile.Info.OpenRead();
                using var image = SKImage.FromEncodedData(imageStream);
                using var destinationStream = destination.Info.OpenWrite();
                image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(destinationStream);
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task WriteImageToInputAsync(
        ImageSource imageSource,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsConnected)
            return;

        if (Client.InputImagesDir is not { } inputImagesDir)
        {
            throw new InvalidOperationException("InputImagesDir is null");
        }

        var inferenceInputs = inputImagesDir.JoinDir("Inference");
        inferenceInputs.Create();
    }

    [MemberNotNull(nameof(Client))]
    private async Task ConnectAsyncImpl(Uri uri, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        IsConnecting = true;
        try
        {
            logger.LogDebug("Connecting to {@Uri}...", uri);

            var tempClient = new ComfyClient(apiFactory, uri);

            await tempClient.ConnectAsync(cancellationToken);
            logger.LogDebug("Connected to {@Uri}", uri);

            Client = tempClient;

            await LoadSharedPropertiesAsync();
        }
        catch (Exception)
        {
            Client = null;
            throw;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return ConnectAsyncImpl(new Uri("http://127.0.0.1:8188"), cancellationToken);
    }

    private async Task MigrateLinksIfNeeded(PackagePair packagePair)
    {
        if (packagePair.InstalledPackage.FullPath is not { } packagePath)
        {
            throw new ArgumentException("Package path is null", nameof(packagePair));
        }

        var inferenceDir = settingsManager.ImagesInferenceDirectory;
        inferenceDir.Create();

        // For locally installed packages only
        // Delete ./output/Inference

        var legacyInferenceLinkDir = new DirectoryPath(packagePair.InstalledPackage.FullPath).JoinDir(
            "output",
            "Inference"
        );

        if (legacyInferenceLinkDir.Exists)
        {
            logger.LogInformation("Deleting legacy inference link at {LegacyDir}", legacyInferenceLinkDir);

            if (legacyInferenceLinkDir.IsSymbolicLink)
            {
                await legacyInferenceLinkDir.DeleteAsync(false);
            }
            else
            {
                logger.LogWarning(
                    "Legacy inference link at {LegacyDir} is not a symbolic link, skipping",
                    legacyInferenceLinkDir
                );
            }
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(PackagePair packagePair, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        if (packagePair.BasePackage is not ComfyUI comfyPackage)
        {
            throw new ArgumentException("Base package is not ComfyUI", nameof(packagePair));
        }

        // Setup completion provider
        completionProvider
            .Setup()
            .SafeFireAndForget(ex =>
            {
                logger.LogError(ex, "Error setting up completion provider");
            });

        await MigrateLinksIfNeeded(packagePair);

        // Get user defined host and port
        var host = packagePair.InstalledPackage.GetLaunchArgsHost();
        if (string.IsNullOrWhiteSpace(host))
        {
            host = "127.0.0.1";
        }
        host = host.Replace("localhost", "127.0.0.1");

        var port = packagePair.InstalledPackage.GetLaunchArgsPort();
        if (string.IsNullOrWhiteSpace(port))
        {
            port = "8188";
        }

        var uri = new UriBuilder("http", host, int.Parse(port)).Uri;

        await ConnectAsyncImpl(uri, cancellationToken);

        Client.LocalServerPackage = packagePair;
        Client.LocalServerPath = packagePair.InstalledPackage.FullPath!;
    }

    public async Task CloseAsync()
    {
        if (!IsConnected)
            return;

        await Client.CloseAsync();
        Client = null;
        ResetSharedProperties();
    }

    public void Dispose()
    {
        Client?.Dispose();
        Client = null;
        GC.SuppressFinalize(this);
    }

    ~InferenceClientManager()
    {
        Dispose();
    }
}
