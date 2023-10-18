using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackEditableCard))]
public partial class StackEditableCardViewModel : StackViewModelBase
{
    private readonly ServiceManager<ViewModelBase> vmFactory;

    /// <summary>
    /// Available module types for user creation
    /// </summary>
    public IReadOnlyList<Type> AvailableModules { get; set; } = Array.Empty<Type>();

    /// <summary>
    /// Default modules that are used when no modules are loaded
    /// This is a subset of <see cref="AvailableModules"/>
    /// </summary>
    public IReadOnlyList<Type> DefaultModules { get; set; } = Array.Empty<Type>();

    /// <inheritdoc />
    public StackEditableCardViewModel(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        this.vmFactory = vmFactory;
    }

    public void InitializeDefaults()
    {
        AddCards(DefaultModules.Select(t => vmFactory.Get(t)).Cast<LoadableViewModelBase>());
    }

    [RelayCommand]
    private void AddModule(Type type)
    {
        if (!type.IsSubclassOf(typeof(ModuleBase)))
        {
            throw new ArgumentException($"Type {type} must be subclass of {nameof(ModuleBase)}");
        }
        var card = vmFactory.Get(type) as LoadableViewModelBase;
        AddCards(card!);
    }

    /*/// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var derivedTypes = ViewModelSerializer.GetDerivedTypes(typeof(LoadableViewModelBase));
        
        Clear();
        
        var stateArray = state.AsArray();

        foreach (var node in stateArray)
        {
            
        }
        
        var cards = ViewModelSerializer.DeserializeJsonObject<List<LoadableViewModelBase>>(state);
        AddCards(cards!);
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return ViewModelSerializer.SerializeToJsonObject(Cards.ToList());
    }*/
}
