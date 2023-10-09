using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackEditableCard))]
public partial class StackEditableCardViewModel : StackViewModelBase
{
    private readonly ServiceManager<ViewModelBase> vmFactory;

    /// <summary>
    /// Available card types resolvers for creation / serialization
    /// - The Types must be annotated with <see cref="PolymorphicKeyAttribute"/>
    /// </summary>
    public IReadOnlyList<EditableModule> AvailableModules { get; } = Array.Empty<EditableModule>();

    public IReadOnlyList<EditableModule> DefaultModules { get; } = Array.Empty<EditableModule>();

    public StackEditableCardViewModel(ServiceManager<ViewModelBase> vmFactory)
    {
        this.vmFactory = vmFactory;
    }

    private ViewModelBase? GetViewModelFromTypeKey(string key)
    {
        return AvailableModules.FirstOrDefault(x => x.Value == key)?.Builder(vmFactory);
    }

    private EditableModule? GetModuleFromViewModel(ViewModelBase vm)
    {
        return AvailableModules.FirstOrDefault(x => x.Builder(vmFactory).GetType() == vm.GetType());
    }

    private void AppendNewCard<T>()
        where T : LoadableViewModelBase
    {
        var card = vmFactory.Get<T>();
        AddCards(card);
    }

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
        var cards = Cards
            .Select(x =>
            {
                // Add $type to each card
                var type = x.GetType().FullName;
                return x.SaveStateToJsonObject();
            })
            .ToList();

        return SerializeModel(new StackCardModel { Cards = cards });
    }
}
