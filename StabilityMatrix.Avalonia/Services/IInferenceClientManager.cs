using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using StabilityMatrix.Core.Inference;

namespace StabilityMatrix.Avalonia.Services;

public interface IInferenceClientManager : IDisposable
{
    ComfyClient? Client { get; set; }
    
    [MemberNotNullWhen(true, nameof(Client))]
    bool IsConnected { get; }
    
    List<string>? ModelNames { get; set; }
    List<string>? Samplers { get; set; }
    
    Task ConnectAsync();

    Task CloseAsync();
}
