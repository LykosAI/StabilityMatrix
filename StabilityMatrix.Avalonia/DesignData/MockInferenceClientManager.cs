using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Inference;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockInferenceClientManager : IInferenceClientManager
{
    public ComfyClient? Client { get; set; }
    
    public List<string>? ModelNames { get; set; }
    public List<string>? Samplers { get; set; } = new()
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
