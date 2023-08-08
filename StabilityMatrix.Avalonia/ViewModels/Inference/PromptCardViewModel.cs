using AvaloniaEdit.Document;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptCard))]
public class PromptCardViewModel : ViewModelBase, ILoadableState<PromptCardModel>
{
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    /// <inheritdoc />
    public void LoadState(PromptCardModel state)
    {
        PromptDocument.Text = state.Prompt ?? "";
        NegativePromptDocument.Text = state.NegativePrompt ?? "";
    }

    /// <inheritdoc />
    public PromptCardModel SaveState()
    {
        return new PromptCardModel
        {
            Prompt = PromptDocument.Text,
            NegativePrompt = NegativePromptDocument.Text
        };
    }
}
