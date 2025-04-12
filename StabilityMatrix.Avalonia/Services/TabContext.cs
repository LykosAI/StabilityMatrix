using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Holds shared state scoped to a single Inference tab.
/// </summary>
public partial class TabContext : ObservableObject
{
    [ObservableProperty]
    private HybridModelFile? _selectedModel;

    public event EventHandler<TabStateChangedEventArgs>? StateChanged;

    partial void OnSelectedModelChanged(HybridModelFile? value)
    {
        OnStateChanged(nameof(SelectedModel));
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
