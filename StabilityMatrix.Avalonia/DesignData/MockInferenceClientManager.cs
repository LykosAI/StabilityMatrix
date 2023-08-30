using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockInferenceClientManager : ObservableObject, IInferenceClientManager
{
    public ComfyClient? Client { get; set; }
    
    public IObservableCollection<HybridModelFile> Models { get; } =
        new ObservableCollectionExtended<HybridModelFile>();
    
    public IObservableCollection<HybridModelFile> VaeModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<ComfySampler> Samplers { get; } =
        new ObservableCollectionExtended<ComfySampler>(new ComfySampler[]
        {
            new("euler_ancestral"),
            new("euler"),
            new("lms"),
            new("heun"),
            new("dpm_2"),
            new("dpm_2_ancestral")
        });

    public IObservableCollection<ComfyUpscaler> Upscalers { get; } =
        new ObservableCollectionExtended<ComfyUpscaler>(new ComfyUpscaler[]
        {
            new("nearest-exact", ComfyUpscalerType.Latent),
            new("bicubic", ComfyUpscalerType.Latent),
            new("ESRGAN-4x", ComfyUpscalerType.ESRGAN)
        });

    public bool IsConnected { get; set; }
    
    public Task ConnectAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ConnectAsync(PackagePair packagePair)
    {
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
