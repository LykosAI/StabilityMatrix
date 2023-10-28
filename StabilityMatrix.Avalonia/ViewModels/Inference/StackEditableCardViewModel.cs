using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(StackEditableCard))]
[ManagedService]
[Transient]
public partial class StackEditableCardViewModel : StackViewModelBase
{
    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty]
    [property: JsonIgnore]
    private string? title = Languages.Resources.Label_Steps;

    [ObservableProperty]
    [property: JsonIgnore]
    private bool isEditEnabled;

    /// <summary>
    /// Available module types for user creation
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Type> AvailableModules { get; set; } = Array.Empty<Type>();

    /// <summary>
    /// Default modules that are used when no modules are loaded
    /// This is a subset of <see cref="AvailableModules"/>
    /// </summary>
    [JsonIgnore]
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

    /// <inheritdoc />
    protected override void OnCardAdded(LoadableViewModelBase item)
    {
        base.OnCardAdded(item);

        if (item is ModuleBase module)
        {
            // Inherit our edit state
            module.IsEditEnabled = IsEditEnabled;
        }
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
