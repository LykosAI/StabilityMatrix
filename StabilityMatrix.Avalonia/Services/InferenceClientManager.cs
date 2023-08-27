using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manager for the current inference client
/// Has observable shared properties for shared info like model names
/// </summary>
public partial class InferenceClientManager : ObservableObject, IInferenceClientManager
{
    private readonly ILogger<InferenceClientManager> logger;
    private readonly IApiFactory apiFactory;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsConnected))]
    private ComfyClient? client;

    [MemberNotNullWhen(true, nameof(Client))]
    public bool IsConnected => Client is not null;

    [ObservableProperty]
    private IReadOnlyCollection<string>? modelNames;

    [ObservableProperty]
    private IReadOnlyCollection<ComfySampler>? samplers;

    [ObservableProperty]
    private IReadOnlyCollection<ComfyUpscaler>? upscalers;

    public InferenceClientManager(ILogger<InferenceClientManager> logger, IApiFactory apiFactory)
    {
        this.logger = logger;
        this.apiFactory = apiFactory;
        
        ClearSharedProperties();
    }

    private async Task LoadSharedPropertiesAsync()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Client is not connected");

        ModelNames = await Client.GetModelNamesAsync();

        // Fetch sampler names from KSampler node
        var samplerNames = await Client.GetSamplerNamesAsync();
        Samplers = samplerNames?.Select(name => new ComfySampler(name)).ToImmutableArray();

        // Upscalers is latent and esrgan combined
        var upscalerBuilder = ImmutableArray.CreateBuilder<ComfyUpscaler>();
        
        // Add latent upscale methods from LatentUpscale node
        var latentUpscalerNames = await Client.GetNodeOptionNamesAsync(
            "LatentUpscale", 
            "upscale_method");
        if (latentUpscalerNames is not null)
        {
            upscalerBuilder.AddRange(latentUpscalerNames.Select(
                s => new ComfyUpscaler(s, ComfyUpscalerType.Latent)));
        }
        logger.LogTrace("Loaded latent upscale methods: {@Upscalers}", latentUpscalerNames);
        
        // Add Model upscale methods
        var modelUpscalerNames = await Client.GetNodeOptionNamesAsync(
            "UpscaleModelLoader", 
            "model_name");
        if (modelUpscalerNames is not null)
        {
            upscalerBuilder.AddRange(modelUpscalerNames.Select(
                s => new ComfyUpscaler(s, ComfyUpscalerType.ESRGAN)));
        }
        logger.LogTrace("Loaded model upscale methods: {@Upscalers}", modelUpscalerNames);
        
        Upscalers = upscalerBuilder.ToImmutable();
    }

    /// <summary>
    /// Clears shared properties and sets them to local defaults
    /// </summary>
    private void ClearSharedProperties()
    {
        ModelNames = null;

        Samplers = ComfySampler.Defaults;
        
        Upscalers = null;
    }

    public async Task ConnectAsync()
    {
        if (IsConnected)
            return;

        var tempClient = new ComfyClient(apiFactory, new Uri("http://127.0.0.1:8188"));
        await tempClient.ConnectAsync();
        Client = tempClient;
        await LoadSharedPropertiesAsync();
    }

    public async Task ConnectAsync(PackagePair packagePair)
    {
        if (IsConnected)
            return;

        if (packagePair.BasePackage is not ComfyUI)
        {
            throw new ArgumentException("Base package is not ComfyUI", nameof(packagePair));
        }

        var tempClient = new ComfyClient(apiFactory, new Uri("http://127.0.0.1:8188"));

        // Add output dir if available
        if (packagePair.InstalledPackage.FullPath is { } path)
        {
            tempClient.OutputImagesDir = new DirectoryPath(path, "output");
        }

        await tempClient.ConnectAsync();
        Client = tempClient;
        await LoadSharedPropertiesAsync();
    }

    public async Task CloseAsync()
    {
        if (!IsConnected)
            return;

        await Client.CloseAsync();
        Client = null;
        ClearSharedProperties();
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
