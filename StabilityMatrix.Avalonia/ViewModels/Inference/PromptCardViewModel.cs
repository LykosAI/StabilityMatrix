using System.Text.Json.Nodes;
using AvaloniaEdit.Document;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptCard))]
public class PromptCardViewModel : LoadableViewModelBase
{
    public ICompletionProvider CompletionProvider { get; }
    
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    /// <inheritdoc />
    public PromptCardViewModel(ICompletionProvider completionProvider)
    {
        CompletionProvider = completionProvider;
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(new PromptCardModel
        {
            Prompt = PromptDocument.Text,
            NegativePrompt = NegativePromptDocument.Text
        });
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<PromptCardModel>(state);

        PromptDocument.Text = model.Prompt ?? "";
        NegativePromptDocument.Text = model.NegativePrompt ?? "";
    }
}
