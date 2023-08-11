using System.Text.Json.Nodes;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptCard))]
public partial class PromptCardViewModel : LoadableViewModelBase
{
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    [ObservableProperty] private int editorFontSize = 14;
    
    [ObservableProperty] private string editorFontFamily = "Consolas";

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
