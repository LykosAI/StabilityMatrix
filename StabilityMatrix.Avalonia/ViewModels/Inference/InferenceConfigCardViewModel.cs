using System.Collections.Generic;
using Avalonia.Collections;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceConfigCard))]
public class InferenceConfigCardViewModel : ViewModelBase
{
    private readonly ServiceManager<ViewModelBase> viewModelManager = new();
    
    public AvaloniaList<ViewModelBase> ConfigCards { get; } = new();
    
    /// <summary>
    /// Register new cards
    /// </summary>
    public void AddCards(IEnumerable<ViewModelBase> cards)
    {
        foreach (var card in cards)
        {
            viewModelManager.Register(card);
            ConfigCards.Add(card);
        }
    }
    
    /// <summary>
    /// Gets a card by type
    /// </summary>
    public T GetCard<T>() where T : ViewModelBase
    {
        return viewModelManager.Get<T>();
    }
}
