using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackExpander))]
[ManagedService]
[Transient]
public partial class StackExpanderViewModel : StackViewModelBase
{
    [ObservableProperty]
    [property: JsonIgnore]
    private string? title;

    [ObservableProperty]
    private bool isEnabled;

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<StackExpanderModel>(state);
        IsEnabled = model.IsEnabled;

        if (model.Cards is null)
            return;

        foreach (var (i, card) in model.Cards.Enumerate())
        {
            // Ignore if more than cards than we have
            if (i > Cards.Count - 1)
                break;

            Cards[i].LoadStateFromJsonObject(card);
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new StackExpanderModel
            {
                IsEnabled = IsEnabled,
                Cards = Cards.Select(x => x.SaveStateToJsonObject()).ToList()
            }
        );
    }
}
