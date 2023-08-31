using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptCard))]
public partial class PromptCardViewModel : LoadableViewModelBase
{
    public ICompletionProvider CompletionProvider { get; }
    public ITokenizerProvider TokenizerProvider { get; }
    public SharedState SharedState { get; }

    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    [ObservableProperty]
    private bool isAutoCompletionEnabled;

    /// <inheritdoc />
    public PromptCardViewModel(
        ICompletionProvider completionProvider,
        ITokenizerProvider tokenizerProvider,
        ISettingsManager settingsManager,
        SharedState sharedState
    )
    {
        CompletionProvider = completionProvider;
        TokenizerProvider = tokenizerProvider;
        SharedState = sharedState;

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsAutoCompletionEnabled,
            settings => settings.IsPromptCompletionEnabled,
            true
        );
    }

    /// <summary>
    /// Processes current positive prompt text into a Prompt object
    /// </summary>
    public Prompt GetPrompt()
    {
        return Prompt.FromRawText(PromptDocument.Text, TokenizerProvider);
    }

    /// <summary>
    /// Processes current negative prompt text into a Prompt object
    /// </summary>
    public Prompt GetNegativePrompt()
    {
        return Prompt.FromRawText(NegativePromptDocument.Text, TokenizerProvider);
    }

    [RelayCommand]
    private async Task ShowHelpDialog()
    {
        var md = $"""
                  ## {Resources.Label_Emphasis}
                  ```python
                  (keyword)
                  (keyword:weight)
                  ```
                  
                  ## {Resources.Label_Deemphasis}
                  ```python
                  [keyword]
                  ```
                  
                  ## {Resources.Label_EmbeddingsOrTextualInversion}
                  They may be used in either the positive or negative prompts. 
                  Essentially they are text presets, so the position where you place them 
                  could make a difference. 
                  ```python
                  embedding:model
                  (embedding:model:weight)
                  ```
                  
                  ## {Resources.Label_NetworksLoraOrLycoris}
                  Unlike embeddings, network tags do not get tokenized to the model, 
                  so the position in the prompt where you place them does not matter.
                  ```python
                  <lora:model>
                  <lora:model:weight>
                  <lyco:model>
                  <lyco:model:weight>
                  ```
                  
                  ## {Resources.Label_Comments}
                  Inline comments can be marked by a hashtag ‘#’. 
                  All text after a ‘#’ on a line will be disregarded during generation.
                  ```c
                  # comments
                  a red cat # also comments
                  detailed
                  ```
                  """;

        var dialog = DialogHelper.CreateMarkdownDialog(md, "Prompt Syntax");
        dialog.MinDialogWidth = 800;
        dialog.MaxDialogHeight = 1000;
        dialog.MaxDialogWidth = 1000;
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task DebugShowTokens()
    {
        var prompt = GetPrompt();
        var tokens = prompt.TokenizeResult.Tokens;

        var builder = new StringBuilder();

        builder.AppendLine($"## Tokens ({tokens.Length}):");
        builder.AppendLine("```csharp");
        builder.AppendLine(prompt.GetDebugText());
        builder.AppendLine("```");

        try
        {
            var networks = prompt.ExtraNetworks;

            builder.AppendLine($"## Networks ({networks.Count}):");
            builder.AppendLine("```csharp");
            builder.AppendLine(
                JsonSerializer.Serialize(
                    networks,
                    new JsonSerializerOptions() { WriteIndented = true, }
                )
            );
            builder.AppendLine("```");

            builder.AppendLine("## Formatted for server:");
            builder.AppendLine("```csharp");
            builder.AppendLine(prompt.ProcessedText);
            builder.AppendLine("```");
        }
        catch (PromptError e)
        {
            builder.AppendLine($"##{e.GetType().Name} - {e.Message}");
            builder.AppendLine("```csharp");
            builder.AppendLine(e.StackTrace);
            builder.AppendLine("```");
            throw;
        }

        var dialog = DialogHelper.CreateMarkdownDialog(builder.ToString(), "Prompt Tokens");
        dialog.MinDialogWidth = 800;
        dialog.MaxDialogHeight = 1000;
        dialog.MaxDialogWidth = 1000;
        await dialog.ShowAsync();
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new PromptCardModel
            {
                Prompt = PromptDocument.Text,
                NegativePrompt = NegativePromptDocument.Text
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<PromptCardModel>(state);

        PromptDocument.Text = model.Prompt ?? "";
        NegativePromptDocument.Text = model.NegativePrompt ?? "";
    }
}
