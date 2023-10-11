using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
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
    public IReadOnlyList<EditableModule> AvailableModules { get; set; } =
        Array.Empty<EditableModule>();

    /// <summary>
    /// Default modules that are used when no modules are loaded
    /// This is a subset of <see cref="AvailableModules"/>
    /// </summary>
    public IReadOnlyList<EditableModule> DefaultModules { get; set; } =
        Array.Empty<EditableModule>();

    /*[ObservableProperty]
    private IReadOnlyList<EditableModule> currentModules = Array.Empty<EditableModule>();*/

    /// <inheritdoc />
    public StackEditableCardViewModel(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        this.vmFactory = vmFactory;
    }

    public void InitializeDefaults()
    {
        AddCards(DefaultModules.Select(m => m.Build(vmFactory)).Cast<LoadableViewModelBase>());
    }

    /*private ViewModelBase? GetViewModelFromTypeKey(string key)
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
