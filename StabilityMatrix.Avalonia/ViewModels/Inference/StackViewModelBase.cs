using System;
using System.Collections.Generic;
using Avalonia.Collections;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

public abstract class StackViewModelBase : LoadableViewModelBase
{
    private readonly Dictionary<Type, List<LoadableViewModelBase>> viewModelManager = new();
    
    public AvaloniaList<LoadableViewModelBase> Cards { get; } = new();
    
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
    public T GetCard<T>(int index = 0) where T : LoadableViewModelBase
    {
        return (T) viewModelManager[typeof(T)][index];
    }
}
