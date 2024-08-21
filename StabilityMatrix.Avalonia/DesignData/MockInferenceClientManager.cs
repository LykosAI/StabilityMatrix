using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.DesignData;

public partial class MockInferenceClientManager : ObservableObject, IInferenceClientManager
{
    public ComfyClient? Client { get; set; }

    public IObservableCollection<HybridModelFile> Models { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> VaeModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> ControlNetModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> LoraModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> PromptExpansionModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<ComfySampler> Samplers { get; } =
        new ObservableCollectionExtended<ComfySampler>(ComfySampler.Defaults);

    public IObservableCollection<ComfyUpscaler> Upscalers { get; } =
        new ObservableCollectionExtended<ComfyUpscaler>(
            ComfyUpscaler.Defaults.Concat(ComfyUpscaler.DefaultDownloadableModels)
        );

    public IObservableCollection<ComfyScheduler> Schedulers { get; } =
        new ObservableCollectionExtended<ComfyScheduler>(ComfyScheduler.Defaults);

    public IObservableCollection<ComfyAuxPreprocessor> Preprocessors { get; } =
        new ObservableCollectionExtended<ComfyAuxPreprocessor>(ComfyAuxPreprocessor.Defaults);

    public IObservableCollection<HybridModelFile> UltralyticsModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> SamModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> UnetModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

    public IObservableCollection<HybridModelFile> ClipModels { get; } =
        new ObservableCollectionExtended<HybridModelFile>();

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

    public MockInferenceClientManager()
    {
        Models.AddRange(
            new[]
            {
                HybridModelFile.FromRemote("v1-5-pruned-emaonly.safetensors"),
                HybridModelFile.FromRemote("artshaper1.safetensors"),
            }
        );
    }

    /// <inheritdoc />
    public Task CopyImageToInputAsync(FilePath imageFile, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UploadInputImageAsync(ImageSource image, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task WriteImageToInputAsync(ImageSource imageSource, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

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
