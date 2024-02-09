using System;
using System.Diagnostics.CodeAnalysis;
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
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
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
    }

    /// <summary>
    /// Clears shared properties and sets them to local defaults
    /// </summary>
    private void ResetSharedProperties()
    {
        // Load local models
        modelsSource.EditDiff(
            modelIndexService
                .GetFromModelIndex(SharedFolderType.StableDiffusion)
                .Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Load local control net models
        controlNetModelsSource.EditDiff(
            modelIndexService
                .GetFromModelIndex(SharedFolderType.ControlNet)
                .Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        // Downloadable ControlNet models
        var downloadableControlNets = RemoteModels.ControlNetModels.Where(
            u => !controlNetModelsSource.Lookup(u.GetId()).HasValue
        );
        downloadableControlNetModelsSource.EditDiff(downloadableControlNets, HybridModelFile.Comparer);

        // Load local VAE models
        vaeModelsSource.EditDiff(
            modelIndexService.GetFromModelIndex(SharedFolderType.VAE).Select(HybridModelFile.FromLocal),
            HybridModelFile.Comparer
        );

        samplersSource.EditDiff(ComfySampler.Defaults, ComfySampler.Comparer);

        latentUpscalersSource.EditDiff(ComfyUpscaler.Defaults, ComfyUpscaler.Comparer);

        schedulersSource.EditDiff(ComfyScheduler.Defaults, ComfyScheduler.Comparer);

        // Load Upscalers
        modelUpscalersSource.EditDiff(
            modelIndexService
                .GetFromModelIndex(
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
    }

    /// <inheritdoc />
    public async Task UploadInputImageAsync(ImageSource image, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        if (image.LocalFile is not { } localFile)
        {
            throw new ArgumentException("Image is not a local file", nameof(image));
        }

        var uploadName = await image.GetHashGuidFileNameAsync();

        await using var stream = localFile.Info.OpenRead();
        await Client.UploadImageAsync(stream, uploadName, cancellationToken);
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

        var packageDir = new DirectoryPath(packagePair.InstalledPackage.FullPath);

        // Set package paths
        Client!.OutputImagesDir = packageDir.JoinDir("output");
        Client!.InputImagesDir = packageDir.JoinDir("input");
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
