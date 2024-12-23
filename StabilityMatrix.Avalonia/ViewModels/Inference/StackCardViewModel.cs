using System.Linq;
using System.Text.Json.Nodes;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackCard))]
[ManagedService]
[RegisterTransient<StackCardViewModel>]
public class StackCardViewModel : StackViewModelBase
{
    /// <inheritdoc />
    public StackCardViewModel(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory) { }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<StackCardModel>(state);

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
            new StackCardModel { Cards = Cards.Select(x => x.SaveStateToJsonObject()).ToList() }
        );
    }
}
