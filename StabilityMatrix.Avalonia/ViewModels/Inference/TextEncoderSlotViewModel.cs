using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

/// <summary>
/// Represents a single text encoder slot in the dynamic encoder list.
/// </summary>
public partial class TextEncoderSlotViewModel : ViewModelBase
{
    /// <summary>
    /// 1-based index for display (Encoder 1, Encoder 2, etc.)
    /// </summary>
    [ObservableProperty]
    private int index;

    /// <summary>
    /// Display label for this encoder slot.
    /// </summary>
    public string Label => $"Encoder {Index}";

    /// <summary>
    /// The selected CLIP/text encoder model.
    /// </summary>
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    public TextEncoderSlotViewModel() { }

    public TextEncoderSlotViewModel(int index)
    {
        Index = index;
    }

    partial void OnIndexChanged(int value)
    {
        OnPropertyChanged(nameof(Label));
    }
}
