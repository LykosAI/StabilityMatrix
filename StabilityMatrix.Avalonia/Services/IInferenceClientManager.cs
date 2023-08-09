using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Services;

public interface IInferenceClientManager : IDisposable,  INotifyPropertyChanged, INotifyPropertyChanging
{
    ComfyClient? Client { get; set; }
    
    [MemberNotNullWhen(true, nameof(Client))]
    bool IsConnected { get; }
    
    IReadOnlyCollection<string>? ModelNames { get; set; }
    IReadOnlyCollection<ComfySampler>? Samplers { get; set; }
    IReadOnlyCollection<ComfyUpscaler>? Upscalers { get; set; }
    
    Task ConnectAsync();

    Task ConnectAsync(PackagePair packagePair);

    Task CloseAsync();
}
