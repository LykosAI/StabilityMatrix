using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Holds shared state scoped to a single Inference tab.
/// </summary>
[RegisterScoped<TabContext>]
[ManagedService]
public partial class TabContext : ObservableObject
{
    [ObservableProperty]
    private HybridModelFile? _selectedModel;

    /// <summary>
    /// Current sampler/generation width.
    /// </summary>
    [ObservableProperty]
    private int _samplerWidth = 1024;

    /// <summary>
    /// Current sampler/generation height.
    /// </summary>
    [ObservableProperty]
    private int _samplerHeight = 1024;

    /// <summary>
    /// Width of the primary source image for the current inference tab.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceImageDimensions))]
    private int _sourceImageWidth;

    /// <summary>
    /// Height of the primary source image for the current inference tab.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceImageDimensions))]
    private int _sourceImageHeight;

    public bool HasSourceImageDimensions => SourceImageWidth > 0 && SourceImageHeight > 0;

    public event EventHandler<TabStateChangedEventArgs>? StateChanged;

    partial void OnSelectedModelChanged(HybridModelFile? value)
    {
        OnStateChanged(nameof(SelectedModel));
    }

    partial void OnSamplerWidthChanged(int value)
    {
        OnStateChanged(nameof(SamplerWidth));
    }

    partial void OnSamplerHeightChanged(int value)
    {
        OnStateChanged(nameof(SamplerHeight));
    }

    partial void OnSourceImageWidthChanged(int value)
    {
        OnStateChanged(nameof(SourceImageWidth));
    }

    partial void OnSourceImageHeightChanged(int value)
    {
        OnStateChanged(nameof(SourceImageHeight));
    }

    protected virtual void OnStateChanged(string propertyName)
    {
        StateChanged?.Invoke(this, new TabStateChangedEventArgs(propertyName));
    }

    public class TabStateChangedEventArgs(string propertyName) : EventArgs
    {
        public string PropertyName { get; } = propertyName;
    }
}
