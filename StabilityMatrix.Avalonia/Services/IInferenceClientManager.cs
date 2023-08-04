using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using StabilityMatrix.Core.Inference;

namespace StabilityMatrix.Avalonia.Services;

public interface IInferenceClientManager : IDisposable,  INotifyPropertyChanged, INotifyPropertyChanging
{
    ComfyClient? Client { get; set; }
    
    [MemberNotNullWhen(true, nameof(Client))]
    bool IsConnected { get; }
    
    IReadOnlyCollection<string>? ModelNames { get; set; }
    IReadOnlyCollection<string>? Samplers { get; set; }
    
    Task ConnectAsync();

    Task CloseAsync();
}
