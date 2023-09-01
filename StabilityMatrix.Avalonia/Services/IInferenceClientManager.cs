using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using DynamicData.Binding;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Services;

public interface IInferenceClientManager
    : IDisposable,
        INotifyPropertyChanged,
        INotifyPropertyChanging
{
    ComfyClient? Client { get; set; }

    [MemberNotNullWhen(true, nameof(Client))]
    bool IsConnected { get; }

    IObservableCollection<HybridModelFile> Models { get; }
    IObservableCollection<HybridModelFile> VaeModels { get; }
    IObservableCollection<ComfySampler> Samplers { get; }
    IObservableCollection<ComfyUpscaler> Upscalers { get; }
    IObservableCollection<ComfyScheduler> Schedulers { get; }

    Task ConnectAsync();

    Task ConnectAsync(PackagePair packagePair);

    Task CloseAsync();
}
