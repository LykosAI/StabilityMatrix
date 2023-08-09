using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackExpander))]
public partial class StackExpanderViewModel : StackViewModelBase
{
    [ObservableProperty] private string? title;
    [ObservableProperty] private bool isEnabled;
    
    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<StackExpanderModel>(state);
        Title = model.Title;
        IsEnabled = model.IsEnabled;
        
        if (model.Cards is null) return;
        
        foreach (var (i, card) in model.Cards.Enumerate())
        {
            // Ignore if more than cards than we have
            if (i > Cards.Count - 1) break;
            
            Cards[i].LoadStateFromJsonObject(card);
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(new StackExpanderModel
        {
            Title = Title,
            IsEnabled = IsEnabled,
            Cards = Cards.Select(x => x.SaveStateToJsonObject()).ToList()
        });
    }
}
