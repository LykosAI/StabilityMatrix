using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockInferenceClientManager : ObservableObject, IInferenceClientManager
{
    public ComfyClient? Client { get; set; }
    
    public IReadOnlyCollection<string>? ModelNames { get; set; }
    public IReadOnlyCollection<ComfySampler>? Samplers { get; set; } = new ComfySampler[]
    {
        new("euler_ancestral"),
        new("euler"),
        new("lms"),
        new("heun"),
        new("dpm_2"),
        new("dpm_2_ancestral")
    };
    
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
