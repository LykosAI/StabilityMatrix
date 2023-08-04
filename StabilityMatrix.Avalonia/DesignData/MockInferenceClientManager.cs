using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Inference;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockInferenceClientManager : ObservableObject, IInferenceClientManager
{
    public ComfyClient? Client { get; set; }
    
    public IReadOnlyCollection<string>? ModelNames { get; set; }
    public IReadOnlyCollection<string>? Samplers { get; set; } = new[]
    {
        "Euler a",
        "Euler",
        "LMS",
        "Heun",
        "DPM2",
        "DPM2 a",
        "DPM++ 2S a",
    };
    
    public bool IsConnected { get; set; }
    
    public Task ConnectAsync()
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
