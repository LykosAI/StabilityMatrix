using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.DesignData;

public partial class MockInferenceClientManager : ObservableObject, IInferenceClientManager
{
    public ComfyClient? Client { get; set; }

    public IObservableCollection<HybridModelFile> Models { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> VaeModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<ComfySampler> Samplers { get; } =
        new ObservableCollectionExtended<ComfySampler>(ComfySampler.Defaults);

    public IObservableCollection<ComfyUpscaler> Upscalers { get; } =
        new ObservableCollectionExtended<ComfyUpscaler>(
            new ComfyUpscaler[]
            {
                new("nearest-exact", ComfyUpscalerType.Latent),
                new("bicubic", ComfyUpscalerType.Latent),
                new("ESRGAN-4x", ComfyUpscalerType.ESRGAN)
            }
        );

    public IObservableCollection<ComfyScheduler> Schedulers { get; } =
        new ObservableCollectionExtended<ComfyScheduler>(ComfyScheduler.Defaults);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUserConnect))]
    private bool isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUserConnect))]
    private bool isConnecting;

    /// <inheritdoc />
    public bool CanUserConnect => !IsConnected && !IsConnecting;

    /// <inheritdoc />
    public bool CanUserDisconnect => IsConnected && !IsConnecting;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnecting = true;
        await Task.Delay(5000, cancellationToken);

        IsConnecting = false;
        IsConnected = true;
    }

    /// <inheritdoc />
    public Task ConnectAsync(PackagePair packagePair, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
