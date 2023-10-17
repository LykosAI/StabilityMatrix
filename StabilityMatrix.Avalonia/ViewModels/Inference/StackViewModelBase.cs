using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia.Collections;
using Nito.Disposables.Internals;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

public abstract class StackViewModelBase : LoadableViewModelBase
{
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly Dictionary<Type, List<LoadableViewModelBase>> viewModelManager = new();

    public AvaloniaList<LoadableViewModelBase> Cards { get; } = new();

    protected StackViewModelBase(ServiceManager<ViewModelBase> vmFactory)
    {
        this.vmFactory = vmFactory;

        Cards.CollectionChanged += (sender, args) =>
        {
            if (args.NewItems != null)
            {
                var itemIndex = args.NewStartingIndex;
                foreach (var item in args.NewItems.OfType<StackViewModelBase>())
                {
                    item.OnContainerIndexChanged(itemIndex);
                    itemIndex++;
                }
            }
        };
    }

    public virtual void OnContainerIndexChanged(int value) { }

    public void AddCards(params LoadableViewModelBase[] cards)
    {
        AddCards((IEnumerable<LoadableViewModelBase>)cards);
    }

    /// <summary>
    /// Register new cards
    /// </summary>
    public void AddCards(IEnumerable<LoadableViewModelBase> cards)
    {
        foreach (var card in cards)
        {
            var list = viewModelManager.GetOrAdd(card.GetType());
            list.Add(card);
            Cards.Add(card);
        }
    }

    /// <summary>
    /// Registers new cards and returns self
    /// </summary>
    public StackViewModelBase WithCards(IEnumerable<LoadableViewModelBase> cards)
    {
        AddCards(cards);
        return this;
    }

    /// <summary>
    /// Gets a card by type at specified index
    /// </summary>
    public T GetCard<T>(int index = 0)
        where T : LoadableViewModelBase
    {
        return (T)viewModelManager[typeof(T)][index];
    }

    public void Clear()
    {
        Cards.Clear();
        viewModelManager.Clear();
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        Clear();

        var derivedTypes = ViewModelSerializer.GetDerivedTypes(typeof(LoadableViewModelBase));

        if (
            !state.TryGetPropertyValue("$values", out var values)
            || values is not JsonArray nodesArray
        )
        {
            return;
        }

        foreach (var node in nodesArray.Select(n => n as JsonObject).WhereNotNull())
        {
            // Get $type key
            if (
                !node.TryGetPropertyValue("$type", out var typeValue)
                || typeValue is not JsonValue jsonValue
                || jsonValue.ToString() is not { } typeKey
            )
            {
                continue;
            }

            // Get type from key
            if (!derivedTypes.TryGetValue(typeKey, out var type))
            {
                continue;
            }

            if (vmFactory.Get(type) is not LoadableViewModelBase vm)
            {
                continue;
            }

            vm.LoadStateFromJsonObject(node);
            AddCards(vm);
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        var derivedTypeNames = ViewModelSerializer
            .GetDerivedTypes(typeof(LoadableViewModelBase))
            .ToDictionary(x => x.Value, x => x.Key);

        var nodes = new JsonArray(
            Cards
                .Select(x =>
                {
                    var typeKey = derivedTypeNames[x.GetType()];
                    var node = x.SaveStateToJsonObject();
                    node.Add("$type", typeKey);
                    return (JsonNode)node;
                })
                .ToArray()
        );

        return new JsonObject { ["$values"] = nodes };
    }
}
