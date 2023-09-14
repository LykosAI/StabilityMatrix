using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Api;
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
public partial class InferenceClientManager : ObservableObject, IInferenceClientManager
{
    private readonly ILogger<InferenceClientManager> logger;
    private readonly IApiFactory apiFactory;
    private readonly IModelIndexService modelIndexService;
    private readonly ISettingsManager settingsManager;

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

    private readonly SourceCache<ComfySampler, string> samplersSource = new(p => p.Name);

    public IObservableCollection<ComfySampler> Samplers { get; } =
        new ObservableCollectionExtended<ComfySampler>();

    private readonly SourceCache<ComfyUpscaler, string> modelUpscalersSource = new(p => p.Name);

    private readonly SourceCache<ComfyUpscaler, string> latentUpscalersSource = new(p => p.Name);

    public IObservableCollection<ComfyUpscaler> Upscalers { get; } =
        new ObservableCollectionExtended<ComfyUpscaler>();

    private readonly SourceCache<ComfyScheduler, string> schedulersSource = new(p => p.Name);

    public IObservableCollection<ComfyScheduler> Schedulers { get; } =
        new ObservableCollectionExtended<ComfyScheduler>();

    public InferenceClientManager(
        ILogger<InferenceClientManager> logger,
        IApiFactory apiFactory,
        IModelIndexService modelIndexService,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.apiFactory = apiFactory;
        this.modelIndexService = modelIndexService;
        this.settingsManager = settingsManager;

        modelsSource.Connect().DeferUntilLoaded().Bind(Models).Subscribe();

        vaeModelsDefaults.AddOrUpdate(HybridModelFile.Default);

        vaeModelsDefaults
            .Connect()
            .Or(vaeModelsSource.Connect())
            .DeferUntilLoaded()
            .Bind(VaeModels)
            .Subscribe();

        samplersSource.Connect().DeferUntilLoaded().Bind(Samplers).Subscribe();

        latentUpscalersSource
            .Connect()
            .Or(modelUpscalersSource.Connect())
            .DeferUntilLoaded()
            .Bind(Upscalers)
            .Subscribe();

        schedulersSource.Connect().DeferUntilLoaded().Bind(Schedulers).Subscribe();

        ResetSharedProperties();

        EventManager.Instance.ModelIndexChanged += (s, e) =>
        {
            logger.LogDebug("Model index changed, reloading shared properties for Inference");
            if (IsConnected)
            {
                LoadSharedPropertiesAsync()
                    .SafeFireAndForget(
                        onException: ex => logger.LogError(ex, "Error loading shared properties")
                    );
            }
            else
            {
                ResetSharedProperties();
            }
        };
    }

    private async Task LoadSharedPropertiesAsync()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Client is not connected");

        if (await Client.GetModelNamesAsync() is { } modelNames)
        {
            modelsSource.EditDiff(
                modelNames.Select(HybridModelFile.FromRemote),
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
            await Client.GetNodeOptionNamesAsync("LatentUpscale", "upscale_method") is
            { } latentUpscalerNames
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
            await Client.GetNodeOptionNamesAsync("UpscaleModelLoader", "model_name") is
            { } modelUpscalerNames
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
            schedulersSource.EditDiff(
                schedulerNames.Select(s => new ComfyScheduler(s)),
                ComfyScheduler.Comparer
            );
            logger.LogTrace("Loaded scheduler methods: {@Schedulers}", schedulerNames);
        }
    }

    /// <summary>
    /// Clears shared properties and sets them to local defaults
    /// </summary>
    private void ResetSharedProperties()
    {
        // Load local models
        modelIndexService
            .GetModelsOfType(SharedFolderType.StableDiffusion)
            .ContinueWith(task =>
            {
                modelsSource.EditDiff(
                    task.Result.Select(HybridModelFile.FromLocal),
                    HybridModelFile.Comparer
                );
            })
            .SafeFireAndForget();

        // Load local VAE models
        modelIndexService
            .GetModelsOfType(SharedFolderType.VAE)
            .ContinueWith(task =>
            {
                vaeModelsSource.EditDiff(
                    task.Result.Select(HybridModelFile.FromLocal),
                    HybridModelFile.Comparer
                );
            })
            .SafeFireAndForget();

        samplersSource.EditDiff(ComfySampler.Defaults, ComfySampler.Comparer);

        latentUpscalersSource.EditDiff(ComfyUpscaler.Defaults, ComfyUpscaler.Comparer);

        modelUpscalersSource.Clear();

        schedulersSource.EditDiff(ComfyScheduler.Defaults, ComfyScheduler.Comparer);
    }

    /// <inheritdoc />
    public async Task CopyImageToInputAsync(
        FilePath imageFile,
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

        var destination = inferenceInputs.JoinFile(imageFile.Name);

        // Read to SKImage then write to file, to prevent errors from metadata
        await using var imageStream = imageFile.Info.OpenRead();
        using var image = SKImage.FromEncodedData(imageStream);
        await using var destinationStream = destination.Info.OpenWrite();
        image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(destinationStream);
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
            Client = tempClient;
            logger.LogDebug("Connected to {@Uri}", uri);

            await LoadSharedPropertiesAsync();
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

    /// <inheritdoc />
    public async Task ConnectAsync(
        PackagePair packagePair,
        CancellationToken cancellationToken = default
    )
    {
        if (IsConnected)
            return;

        if (packagePair.BasePackage is not ComfyUI comfyPackage)
        {
            throw new ArgumentException("Base package is not ComfyUI", nameof(packagePair));
        }

        // Setup image folder links
        await comfyPackage.SetupInferenceOutputFolderLinks(
            packagePair.InstalledPackage.FullPath
                ?? throw new InvalidOperationException("Package does not have a Path")
        );

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
