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

    private HybridModelFile? selectedModel;

    /// <summary>
    /// The selected CLIP/text encoder model.
    /// </summary>
    public HybridModelFile? SelectedModel
    {
        get => selectedModel;
        set
        {
            // The bound ComboBox can briefly report null while the model list refreshes
            // (e.g. when navigating away and back to the Inference tab). Ignore the
            // transient null so the encoder selection isn't cleared out from under the user.
            if (value is null && selectedModel is not null)
            {
                return;
            }

            SetProperty(ref selectedModel, value);
        }
    }

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
