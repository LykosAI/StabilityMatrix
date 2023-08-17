using System.Diagnostics;
using System.Text.Json.Nodes;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptCard))]
public partial class PromptCardViewModel : LoadableViewModelBase
{
    public ICompletionProvider CompletionProvider { get; }
    
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    [ObservableProperty]
    private bool isAutoCompletionEnabled;
    
    /// <inheritdoc />
    public PromptCardViewModel(ICompletionProvider completionProvider, ISettingsManager settingsManager)
    {
        CompletionProvider = completionProvider;
        
        settingsManager.RelayPropertyFor(this,
            vm => vm.IsAutoCompletionEnabled,
            settings => settings.IsPromptCompletionEnabled,
            true);
    }
    
    partial void OnIsAutoCompletionEnabledChanged(bool value)
    {
        Debug.WriteLine("OnIsAutoCompletionEnabledChanged: " + value);
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
