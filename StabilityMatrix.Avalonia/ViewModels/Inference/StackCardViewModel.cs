using System;
using System.Collections.Generic;
using Avalonia.Collections;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackCard))]
public class StackCardViewModel : ViewModelBase
{
    private readonly Dictionary<Type, List<ViewModelBase>> viewModelManager = new();
    
    public AvaloniaList<ViewModelBase> ConfigCards { get; } = new();
    
    /// <summary>
    /// Register new cards
    /// </summary>
    public void AddCards(IEnumerable<ViewModelBase> cards)
    {
        foreach (var card in cards)
        {
            var list = viewModelManager.GetOrAdd(card.GetType());
            list.Add(card);
            ConfigCards.Add(card);
        }
    }
    
    /// <summary>
    /// Registers new cards and returns self
    /// </summary>
    public StackCardViewModel WithCards(IEnumerable<ViewModelBase> cards)
    {
        AddCards(cards);
        return this;
    }
    
    /// <summary>
    /// Gets a card by type at specified index
    /// </summary>
    public T GetCard<T>(int index = 0) where T : ViewModelBase
    {
        return (T) viewModelManager[typeof(T)][index];
    }
}
