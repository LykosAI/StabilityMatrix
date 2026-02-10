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

    protected virtual void OnStateChanged(string propertyName)
    {
        StateChanged?.Invoke(this, new TabStateChangedEventArgs(propertyName));
    }

    public class TabStateChangedEventArgs(string propertyName) : EventArgs
    {
        public string PropertyName { get; } = propertyName;
    }
}
