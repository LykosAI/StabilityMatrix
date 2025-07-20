using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Services;

public interface IInferenceClientManager : IDisposable, INotifyPropertyChanged, INotifyPropertyChanging
{
    ComfyClient? Client { get; set; }

    /// <summary>
    /// Whether the client is connected
    /// </summary>
    [MemberNotNullWhen(true, nameof(Client))]
    bool IsConnected { get; }

    /// <summary>
    /// Whether the client is connecting
    /// </summary>
    bool IsConnecting { get; }

    /// <summary>
    /// Whether the user can initiate a connection
    /// </summary>
    bool CanUserConnect { get; }

    /// <summary>
    /// Whether the user can initiate a disconnection
    /// </summary>
    bool CanUserDisconnect { get; }

    IObservableCollection<HybridModelFile> Models { get; }
    IObservableCollection<HybridModelFile> VaeModels { get; }
    IObservableCollection<HybridModelFile> ControlNetModels { get; }
    IObservableCollection<HybridModelFile> LoraModels { get; }
    IObservableCollection<HybridModelFile> PromptExpansionModels { get; }
    IObservableCollection<ComfySampler> Samplers { get; }
    IObservableCollection<ComfyUpscaler> Upscalers { get; }
    IObservableCollection<ComfyScheduler> Schedulers { get; }
    IObservableCollection<ComfyAuxPreprocessor> Preprocessors { get; }
    IObservableCollection<HybridModelFile> UltralyticsModels { get; }
    IObservableCollection<HybridModelFile> SamModels { get; }
    IObservableCollection<HybridModelFile> UnetModels { get; }
    IObservableCollection<HybridModelFile> ClipModels { get; }
    IObservableCollection<HybridModelFile> ClipVisionModels { get; }
    IObservable<IChangeSet<HybridModelFile, string>> LoraModelsChangeSet { get; }

    Task CopyImageToInputAsync(FilePath imageFile, CancellationToken cancellationToken = default);

    Task UploadInputImageAsync(ImageSource image, CancellationToken cancellationToken = default);

    Task WriteImageToInputAsync(ImageSource imageSource, CancellationToken cancellationToken = default);

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task ConnectAsync(PackagePair packagePair, CancellationToken cancellationToken = default);

    Task CloseAsync();
}
